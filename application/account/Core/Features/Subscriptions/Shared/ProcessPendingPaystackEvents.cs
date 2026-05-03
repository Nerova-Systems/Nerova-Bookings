using System.Data;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Integrations.Paystack;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Shared;

/// <summary>
///     Phase 2 of two-phase webhook processing. Acquires a pessimistic lock on the subscription row
///     to serialize concurrent webhook processing, syncs current state from Paystack, then applies
///     side effects (tenant state changes) based on state diffs between local and synced data.
/// </summary>
public sealed class ProcessPendingPaystackEvents(
    AccountDbContext dbContext,
    ISubscriptionRepository subscriptionRepository,
    IPaystackEventRepository paystackEventRepository,
    ITenantRepository tenantRepository,
    PaystackClientFactory paystackClientFactory,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events,
    TelemetryClient telemetryClient,
    ILogger<ProcessPendingPaystackEvents> logger
)
{
    public async Task ExecuteAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        // Pessimistic lock serializes concurrent webhook processing for the same customer
        var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        await using var transaction = isSqlite
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var subscription = await subscriptionRepository.GetByPaystackCustomerIdWithLockUnfilteredAsync(paystackCustomerId, cancellationToken);
        if (subscription is null)
        {
            logger.LogWarning("Subscription not found for Paystack customer '{PaystackCustomerId}', events will be retried on next webhook", paystackCustomerId);
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var tenant = (await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken))!;
        var pendingEvents = await paystackEventRepository.GetPendingByPaystackCustomerIdAsync(paystackCustomerId, cancellationToken);

        if (pendingEvents.Length > 0)
        {
            await SyncStateFromPaystack(tenant, subscription, cancellationToken);

            MarkAllEventsAsProcessed(pendingEvents, subscription);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        SendTelemetryEvents(tenant, subscription);
    }

    private async Task SyncStateFromPaystack(Tenant tenant, Subscription subscription, CancellationToken cancellationToken)
    {
        // Fetch current state from Paystack
        var paystackClient = paystackClientFactory.GetClient();
        var customerResult = await paystackClient.GetCustomerBillingInfoAsync(subscription.PaystackCustomerId!, cancellationToken);

        var previousPlan = subscription.Plan;
        var previousPriceAmount = subscription.CurrentPriceAmount;
        var previousPriceCurrency = subscription.CurrentPriceCurrency;

        if (customerResult is null)
        {
            logger.LogError("Failed to fetch billing info for Paystack customer '{PaystackCustomerId}'", subscription.PaystackCustomerId);
            return;
        }

        if (customerResult.IsCustomerDeleted)
        {
            subscription.ResetToFreePlan();
            tenant.UpdatePlan(SubscriptionPlan.Basis);
            tenant.Suspend(SuspensionReason.CustomerDeleted, timeProvider.GetUtcNow());
            tenantRepository.Update(tenant);
            subscriptionRepository.Update(subscription);
            events.CollectEvent(new SubscriptionSuspended(subscription.Id, previousPlan, SuspensionReason.CustomerDeleted, previousPriceAmount!.Value, -previousPriceAmount.Value, previousPriceCurrency!));
            return;
        }

        var paystackState = await paystackClient.SyncSubscriptionStateAsync(subscription.PaystackCustomerId!, cancellationToken);

        // Detect state transitions in lifecycle order (variables and if-blocks below follow the same order)
        var billingInfoAdded = subscription.BillingInfo is null && customerResult.BillingInfo is not null;
        var billingInfoUpdated = subscription.BillingInfo is not null && customerResult.BillingInfo is not null && customerResult.BillingInfo != subscription.BillingInfo;
        var latestPaymentMethod = paystackState?.PaymentMethod ?? customerResult.PaymentMethod;
        var paymentMethodUpdated = latestPaymentMethod != subscription.PaymentMethod;
        var subscriptionCreated = subscription.PaystackSubscriptionId is null && paystackState?.PaystackSubscriptionId is not null;
        var subscriptionRenewed = subscription.CurrentPeriodEnd is not null && paystackState?.CurrentPeriodEnd is not null && paystackState.CurrentPeriodEnd > subscription.CurrentPeriodEnd;
        var subscriptionUpgraded = !subscriptionCreated && paystackState is not null && paystackState.Plan != subscription.Plan && paystackState.Plan.IsUpgradeFrom(subscription.Plan);
        var subscriptionCancelled = !subscription.CancelAtPeriodEnd && paystackState?.CancelAtPeriodEnd == true;
        var subscriptionReactivated = subscription.CancelAtPeriodEnd && paystackState?.CancelAtPeriodEnd == false;
        var subscriptionExpired = subscription.PaystackSubscriptionId is not null && paystackState is null && subscription is { CancelAtPeriodEnd: true, FirstPaymentFailedAt: null };
        var subscriptionImmediatelyCancelled = subscription.PaystackSubscriptionId is not null && paystackState is null && subscription is { CancelAtPeriodEnd: false, FirstPaymentFailedAt: null };
        var subscriptionSuspended = subscription.PaystackSubscriptionId is not null && paystackState is null && subscription.FirstPaymentFailedAt is not null;
        var paymentFailed = paystackState?.SubscriptionStatus is PaystackSubscriptionStatus.PastDue or PaystackSubscriptionStatus.Incomplete && subscription.FirstPaymentFailedAt is null;
        var paymentRecovered = paystackState?.SubscriptionStatus == PaystackSubscriptionStatus.Active && subscription.FirstPaymentFailedAt is not null;
        var previousRefundCount = subscription.PaymentTransactions.Count(t => t.Status == PaymentTransactionStatus.Refunded);
        var now = timeProvider.GetUtcNow();
        var daysOnCurrentPlan = (int)(now - (subscription.ModifiedAt ?? subscription.CreatedAt)).TotalDays;

        // Apply Paystack state to aggregate (after detection, before side effects)
        if (paystackState is not null)
        {
            subscription.SetPaystackSubscription(paystackState.PaystackSubscriptionId, paystackState.Plan, paystackState.CurrentPriceAmount, paystackState.CurrentPriceCurrency, paystackState.CurrentPeriodEnd, paystackState.PaymentMethod);
            tenant.UpdatePlan(paystackState.Plan);
        }

        // Always sync payment transactions from Paystack (via subscription when active, via invoices when cancelled)
        var syncedTransactions = paystackState?.PaymentTransactions ?? await paystackClient.SyncPaymentTransactionsAsync(subscription.PaystackCustomerId!, cancellationToken);
        if (syncedTransactions is not null)
        {
            subscription.SetPaymentTransactions([.. syncedTransactions]);
        }

        var paymentRefunded = subscription.PaymentTransactions.Count(t => t.Status == PaymentTransactionStatus.Refunded) > previousRefundCount;

        if (billingInfoAdded)
        {
            subscription.SetBillingInfo(customerResult.BillingInfo);
            events.CollectEvent(new BillingInfoAdded(subscription.Id, customerResult.BillingInfo?.Address?.Country, customerResult.BillingInfo?.Address?.PostalCode, customerResult.BillingInfo?.Address?.City));
        }

        if (billingInfoUpdated)
        {
            subscription.SetBillingInfo(customerResult.BillingInfo);
            events.CollectEvent(new BillingInfoUpdated(subscription.Id, customerResult.BillingInfo?.Address?.Country, customerResult.BillingInfo?.Address?.PostalCode, customerResult.BillingInfo?.Address?.City));
        }

        if (paymentMethodUpdated)
        {
            subscription.SetPaymentMethod(latestPaymentMethod);
            events.CollectEvent(new PaymentMethodUpdated(subscription.Id));
        }

        if (subscriptionCreated)
        {
            tenant.Activate();
            events.CollectEvent(new SubscriptionCreated(subscription.Id, subscription.Plan, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceCurrency!));
        }

        if (subscriptionRenewed)
        {
            events.CollectEvent(new SubscriptionRenewed(subscription.Id, subscription.Plan, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceAmount!.Value - previousPriceAmount!.Value, subscription.CurrentPriceCurrency!));
        }

        if (subscriptionUpgraded)
        {
            events.CollectEvent(new SubscriptionUpgraded(subscription.Id, previousPlan, subscription.Plan, daysOnCurrentPlan, previousPriceAmount!.Value, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceAmount!.Value - previousPriceAmount.Value, subscription.CurrentPriceCurrency!));
        }

        if (subscriptionCancelled)
        {
            subscription.SetCancellation(paystackState!.CancelAtPeriodEnd, paystackState.CancellationReason, paystackState.CancellationFeedback);
            var daysUntilExpiry = subscription.CurrentPeriodEnd is not null ? (int)(subscription.CurrentPeriodEnd.Value - now).TotalDays : (int?)null;
            events.CollectEvent(new SubscriptionCancelled(subscription.Id, subscription.Plan, subscription.CancellationReason ?? CancellationReason.CancelledByAdmin, daysUntilExpiry, daysOnCurrentPlan, subscription.CurrentPriceAmount!.Value, -subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceCurrency!));
        }

        if (subscriptionReactivated)
        {
            var daysSinceCancelled = (int)(now - (subscription.ModifiedAt ?? subscription.CreatedAt)).TotalDays;
            subscription.SetCancellation(paystackState!.CancelAtPeriodEnd, paystackState.CancellationReason, paystackState.CancellationFeedback);
            var daysUntilExpiry = subscription.CurrentPeriodEnd is not null ? (int)(subscription.CurrentPeriodEnd.Value - now).TotalDays : (int?)null;
            events.CollectEvent(new SubscriptionReactivated(subscription.Id, subscription.Plan, daysUntilExpiry, daysSinceCancelled, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceCurrency!));
        }

        if (subscriptionExpired)
        {
            subscription.ResetToFreePlan();
            tenant.UpdatePlan(SubscriptionPlan.Basis);
            events.CollectEvent(new SubscriptionExpired(subscription.Id, previousPlan, daysOnCurrentPlan, previousPriceAmount!.Value, -previousPriceAmount.Value, previousPriceCurrency!));
        }

        if (subscriptionImmediatelyCancelled)
        {
            subscription.ResetToFreePlan();
            tenant.UpdatePlan(SubscriptionPlan.Basis);
            events.CollectEvent(new SubscriptionCancelled(subscription.Id, previousPlan, CancellationReason.CancelledByAdmin, 0, daysOnCurrentPlan, previousPriceAmount!.Value, -previousPriceAmount.Value, previousPriceCurrency!));
        }

        if (subscriptionSuspended)
        {
            subscription.ResetToFreePlan();
            tenant.UpdatePlan(SubscriptionPlan.Basis);
            tenant.Suspend(SuspensionReason.PaymentFailed, timeProvider.GetUtcNow());
            events.CollectEvent(new SubscriptionSuspended(subscription.Id, previousPlan, SuspensionReason.PaymentFailed, previousPriceAmount!.Value, -previousPriceAmount.Value, previousPriceCurrency!));
        }

        if (paymentFailed)
        {
            subscription.SetPaymentFailed(now);
            events.CollectEvent(new PaymentFailed(subscription.Id, subscription.Plan, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceCurrency!));
        }

        if (paymentRecovered)
        {
            var daysInPastDue = (int)(now - subscription.FirstPaymentFailedAt!.Value).TotalDays;
            subscription.ClearPaymentFailure();
            events.CollectEvent(new PaymentRecovered(subscription.Id, subscription.Plan, daysInPastDue, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceCurrency!));
        }

        if (paymentRefunded)
        {
            var refundedTransactions = subscription.PaymentTransactions.Where(t => t.Status == PaymentTransactionStatus.Refunded).ToArray();
            var refundCount = refundedTransactions.Length - previousRefundCount;
            var latestRefund = refundedTransactions[^1];
            var plan = paystackState is not null ? subscription.Plan : previousPlan;
            events.CollectEvent(new PaymentRefunded(subscription.Id, plan, refundCount, latestRefund.Amount, latestRefund.Currency));
        }

        // Persist all aggregate mutations and mark pending events as processed
        var tenantChanged = paystackState is not null || subscriptionCreated || subscriptionExpired || subscriptionImmediatelyCancelled || subscriptionSuspended;
        if (tenantChanged)
        {
            tenantRepository.Update(tenant);
        }

        subscriptionRepository.Update(subscription);
    }

    private void MarkAllEventsAsProcessed(PaystackEvent[] pendingEvents, Subscription subscription)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var pendingEvent in pendingEvents)
        {
            pendingEvent.MarkProcessed(now);
            pendingEvent.SetPaystackSubscriptionId(subscription.PaystackSubscriptionId);
            pendingEvent.SetTenantId(subscription.TenantId);
            paystackEventRepository.Update(pendingEvent);
        }
    }

    private void SendTelemetryEvents(Tenant tenant, Subscription subscription)
    {
        TenantScopedTelemetryContext.Set(tenant.Id, subscription.Plan.ToString());

        // Publish collected telemetry events after successful commit
        while (events.HasEvents)
        {
            var telemetryEvent = events.Dequeue();
            telemetryClient.TrackEvent(telemetryEvent.GetType().Name, telemetryEvent.Properties);
            logger.LogInformation("Telemetry: {EventName} {EventProperties}", telemetryEvent.GetType().Name, string.Join(", ", telemetryEvent.Properties.Select(p => $"{p.Key}={p.Value}")));
        }
    }
}

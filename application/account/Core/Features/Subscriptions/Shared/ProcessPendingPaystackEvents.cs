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
///     to serialize concurrent webhook processing, verifies Paystack transaction references, then applies
///     local lifecycle changes from Nerova payment attempts.
/// </summary>
public sealed class ProcessPendingPaystackEvents(
    AccountDbContext dbContext,
    ISubscriptionRepository subscriptionRepository,
    IPaystackEventRepository paystackEventRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    IBillingEventRepository billingEventRepository,
    ITenantRepository tenantRepository,
    PaystackClientFactory paystackClientFactory,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events,
    TelemetryClient telemetryClient,
    ILogger<ProcessPendingPaystackEvents> logger
)
{
    public async Task<PaystackReconciliationResult> ExecuteAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        await using var transaction = isSqlite
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var subscription = await subscriptionRepository.GetByPaystackCustomerIdWithLockUnfilteredAsync(paystackCustomerId, cancellationToken);
        if (subscription is null)
        {
            logger.LogWarning("Subscription not found for Paystack customer '{PaystackCustomerId}', events will be retried on next webhook", paystackCustomerId);
            await transaction.RollbackAsync(cancellationToken);
            return PaystackReconciliationResult.Empty;
        }

        var tenant = (await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken))!;
        var pendingEvents = await paystackEventRepository.GetPendingByPaystackCustomerIdWithLockAsync(paystackCustomerId, cancellationToken);
        var existingBillingEventIds = await billingEventRepository.GetExistingStripeEventIdsUnfilteredAsync(subscription.Id, cancellationToken);
        var processedReferences = new HashSet<string>(StringComparer.Ordinal);
        var billingEventsAppended = 0;
        var recoveredPaymentAttempts = 0;
        var paystackClient = paystackClientFactory.GetClient();

        foreach (var pendingEvent in pendingEvents)
        {
            var result = await ProcessPendingEventAsync(tenant, subscription, pendingEvent, paystackClient, existingBillingEventIds, cancellationToken);
            if (result.Reference is not null)
            {
                processedReferences.Add(result.Reference);
            }

            billingEventsAppended += result.BillingEventsAppended;
        }

        var pendingAttempts = await paystackPaymentAttemptRepository.GetPendingBySubscriptionIdWithLockUnfilteredAsync(subscription.Id, cancellationToken);
        foreach (var pendingAttempt in pendingAttempts.Where(a => a.PaystackCustomerId == paystackCustomerId && !processedReferences.Contains(a.PaystackReference)))
        {
            var result = await ProcessPendingAttemptAsync(tenant, subscription, pendingAttempt, paystackClient, existingBillingEventIds, cancellationToken);
            billingEventsAppended += result.BillingEventsAppended;
            recoveredPaymentAttempts += result.PaymentAttemptRecovered ? 1 : 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        SendTelemetryEvents(tenant, subscription);
        return new PaystackReconciliationResult(billingEventsAppended, recoveredPaymentAttempts);
    }

    public async Task<bool> DetectAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByPaystackCustomerIdUnfilteredAsync(paystackCustomerId, cancellationToken);
        if (subscription is null)
        {
            logger.LogWarning("Subscription not found for Paystack customer '{PaystackCustomerId}' during drift detection", paystackCustomerId);
            return false;
        }

        var paystackClient = paystackClientFactory.GetClient();
        var customerBilling = await paystackClient.GetCustomerBillingInfoAsync(paystackCustomerId, cancellationToken);
        if (customerBilling is null)
        {
            logger.LogWarning("Paystack customer view unavailable for customer '{PaystackCustomerId}' during drift detection", paystackCustomerId);
            return false;
        }

        var localSnapshot = ProviderBillingSnapshot.FromSubscription(subscription);
        var providerSnapshot = customerBilling.IsCustomerDeleted
            ? new ProviderBillingSnapshot(SubscriptionPlan.Basis, false, null, null)
            : localSnapshot;
        var billingEvents = await billingEventRepository.GetBySubscriptionIdUnfilteredAsync(subscription.Id, cancellationToken);
        var discrepancies = BillingDriftDetector.Detect(localSnapshot, providerSnapshot, subscription.PaymentTransactions.Length, billingEvents.Length);
        await subscriptionRepository.UpdateDriftStatusAsync(subscription.Id, !discrepancies.IsDefaultOrEmpty, timeProvider.GetUtcNow(), discrepancies, cancellationToken);
        return true;
    }

    private async Task<PaystackAttemptProcessingResult> ProcessPendingEventAsync(
        Tenant tenant,
        Subscription subscription,
        PaystackEvent pendingEvent,
        IPaystackClient paystackClient,
        HashSet<string> existingBillingEventIds,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        pendingEvent.SetTenantId(subscription.TenantId);
        pendingEvent.SetPaystackAuthorizationCode(subscription.PaystackAuthorizationCode);

        if (pendingEvent.PaystackReference is null)
        {
            MarkEventFailed(pendingEvent, now, "Paystack webhook did not include a transaction reference.");
            return PaystackAttemptProcessingResult.Empty;
        }

        var paymentAttempt = await paystackPaymentAttemptRepository.GetByReferenceWithLockUnfilteredAsync(pendingEvent.PaystackReference, cancellationToken);
        if (paymentAttempt is null)
        {
            MarkEventFailed(pendingEvent, now, "Paystack payment attempt was not found.");
            return new PaystackAttemptProcessingResult(pendingEvent.PaystackReference, 0, false);
        }

        if (paymentAttempt.SubscriptionId != subscription.Id || paymentAttempt.PaystackCustomerId != subscription.PaystackCustomerId)
        {
            MarkEventFailed(pendingEvent, now, "Paystack payment attempt does not match this subscription.");
            return new PaystackAttemptProcessingResult(paymentAttempt.PaystackReference, 0, false);
        }

        if (paymentAttempt.Status != PaystackPaymentAttemptStatus.Pending)
        {
            MarkEventProcessed(pendingEvent, subscription, now);
            return new PaystackAttemptProcessingResult(paymentAttempt.PaystackReference, 0, false);
        }

        var result = await ProcessPendingAttemptAsync(tenant, subscription, paymentAttempt, paystackClient, existingBillingEventIds, cancellationToken);
        if (result.PaymentAttemptRecovered)
        {
            MarkEventProcessed(pendingEvent, subscription, now);
        }
        else
        {
            MarkEventFailed(pendingEvent, now, paymentAttempt.FailureReason ?? "Paystack payment could not be reconciled.");
        }

        return result with { PaymentAttemptRecovered = false };
    }

    private async Task<PaystackAttemptProcessingResult> ProcessPendingAttemptAsync(
        Tenant tenant,
        Subscription subscription,
        PaystackPaymentAttempt paymentAttempt,
        IPaystackClient paystackClient,
        HashSet<string> existingBillingEventIds,
        CancellationToken cancellationToken
    )
    {
        var now = timeProvider.GetUtcNow();
        var previousPlan = subscription.Plan;
        var previousPriceAmount = subscription.CurrentPriceAmount;

        var verified = await paystackClient.VerifyTransactionAsync(paymentAttempt.PaystackReference, paymentAttempt.Purpose, cancellationToken);
        var validationError = ValidateVerifiedTransaction(subscription, paymentAttempt, verified);
        if (validationError is not null)
        {
            MarkPaymentAttemptFailed(paymentAttempt, now, validationError);
            return new PaystackAttemptProcessingResult(paymentAttempt.PaystackReference, 0, false);
        }

        var processingError = await ApplySuccessfulPaymentAsync(tenant, subscription, paymentAttempt, verified!, paystackClient, now, cancellationToken);
        if (processingError is not null)
        {
            MarkPaymentAttemptFailed(paymentAttempt, now, processingError);
            return new PaystackAttemptProcessingResult(paymentAttempt.PaystackReference, 0, false);
        }

        paymentAttempt.MarkSucceeded(now);
        paystackPaymentAttemptRepository.Update(paymentAttempt);
        var billingEventsAppended = await AppendBillingEventAsync(subscription, paymentAttempt, verified!, previousPlan, previousPriceAmount, now, existingBillingEventIds, cancellationToken);
        return new PaystackAttemptProcessingResult(paymentAttempt.PaystackReference, billingEventsAppended, true);
    }

    private static string? ValidateVerifiedTransaction(Subscription subscription, PaystackPaymentAttempt paymentAttempt, VerifiedPaystackTransactionResult? verified)
    {
        if (verified?.Paid != true)
        {
            return verified?.ErrorMessage ?? "Paystack payment could not be verified.";
        }

        if (!string.Equals(verified.Reference, paymentAttempt.PaystackReference, StringComparison.Ordinal))
        {
            return "Paystack payment reference does not match the pending payment attempt.";
        }

        if (verified.Purpose != paymentAttempt.Purpose)
        {
            return "Paystack payment purpose does not match the pending payment attempt.";
        }

        if (verified.CustomerId is not null && verified.CustomerId != subscription.PaystackCustomerId)
        {
            return "Paystack payment customer does not match this subscription.";
        }

        if (!paymentAttempt.MatchesAmount(verified.Amount, verified.Currency))
        {
            return "Paystack payment amount does not match the pending payment attempt.";
        }

        if (paymentAttempt.Purpose is PaystackPaymentPurpose.Subscribe or PaystackPaymentPurpose.Upgrade && paymentAttempt.Plan is null)
        {
            return "Paystack subscription payment attempt does not include a plan.";
        }

        if (paymentAttempt.Purpose is PaystackPaymentPurpose.Subscribe or PaystackPaymentPurpose.Upgrade or PaystackPaymentPurpose.PaymentMethodAuthorization
            && verified.Authorization is null)
        {
            return "Paystack payment did not return a reusable card authorization.";
        }

        return null;
    }

    private async Task<string?> ApplySuccessfulPaymentAsync(
        Tenant tenant,
        Subscription subscription,
        PaystackPaymentAttempt paymentAttempt,
        VerifiedPaystackTransactionResult verified,
        IPaystackClient paystackClient,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        return paymentAttempt.Purpose switch
        {
            PaystackPaymentPurpose.Subscribe or PaystackPaymentPurpose.Upgrade => ApplySuccessfulSubscriptionPayment(tenant, subscription, paymentAttempt, verified, now),
            PaystackPaymentPurpose.Retry => ApplySuccessfulRetryPayment(tenant, subscription, verified, now),
            PaystackPaymentPurpose.Renewal => ApplySuccessfulRenewalPayment(tenant, subscription, paymentAttempt, verified, now),
            PaystackPaymentPurpose.PaymentMethodAuthorization => await ApplySuccessfulPaymentMethodAuthorizationAsync(subscription, verified, paystackClient, now, cancellationToken),
            _ => "Unsupported Paystack payment purpose."
        };
    }

    private string? ApplySuccessfulSubscriptionPayment(Tenant tenant, Subscription subscription, PaystackPaymentAttempt paymentAttempt, VerifiedPaystackTransactionResult verified, DateTimeOffset now)
    {
        var previousPlan = subscription.Plan;
        var previousPriceAmount = subscription.CurrentPriceAmount;
        var plan = paymentAttempt.Plan!.Value;
        var nextBillingAt = now.AddMonths(1);

        subscription.SetPaystackAuthorization(verified.Authorization!.AuthorizationCode, verified.Authorization.Email, verified.Authorization.Signature, verified.PaymentMethod);
        subscription.SetPaystackBillingState(verified.Authorization.AuthorizationCode, plan, verified.Amount, verified.Currency, now, nextBillingAt, nextBillingAt, verified.PaymentMethod);
        subscription.ClearPaymentFailure();
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), verified.Amount, verified.Amount, 0m, verified.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );

        tenant.UpdatePlan(plan);
        tenant.Activate();
        tenantRepository.Update(tenant);
        subscriptionRepository.Update(subscription);

        if (previousPlan == SubscriptionPlan.Basis)
        {
            events.CollectEvent(new SubscriptionCreated(subscription.Id, plan, verified.Amount, verified.Amount, verified.Currency));
        }
        else if (plan.IsUpgradeFrom(previousPlan))
        {
            events.CollectEvent(new SubscriptionUpgraded(subscription.Id, previousPlan, plan, 0, previousPriceAmount ?? 0m, verified.Amount, verified.Amount - (previousPriceAmount ?? 0m), verified.Currency));
        }

        return null;
    }

    private string? ApplySuccessfulRenewalPayment(Tenant tenant, Subscription subscription, PaystackPaymentAttempt paymentAttempt, VerifiedPaystackTransactionResult verified, DateTimeOffset now)
    {
        var nextBillingAt = now.AddMonths(1);
        var renewalPlan = paymentAttempt.Plan ?? subscription.Plan;
        subscription.StartBillingPeriod(renewalPlan, verified.Amount, verified.Currency, now, nextBillingAt, verified.PaymentMethod);
        subscription.ClearPaymentFailure();
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), verified.Amount, verified.Amount, 0m, verified.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );

        tenant.UpdatePlan(renewalPlan);
        tenant.Activate();
        tenantRepository.Update(tenant);
        subscriptionRepository.Update(subscription);
        events.CollectEvent(new SubscriptionRenewed(subscription.Id, renewalPlan, verified.Amount, verified.Amount, verified.Currency));
        return null;
    }

    private string? ApplySuccessfulRetryPayment(Tenant tenant, Subscription subscription, VerifiedPaystackTransactionResult verified, DateTimeOffset now)
    {
        var nextBillingAt = now.AddMonths(1);
        subscription.ClearPaymentFailure();
        subscription.StartBillingPeriod(subscription.Plan, verified.Amount, verified.Currency, now, nextBillingAt, verified.PaymentMethod);
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), verified.Amount, verified.Amount, 0m, verified.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );

        tenant.UpdatePlan(subscription.Plan);
        tenant.Activate();
        tenantRepository.Update(tenant);
        subscriptionRepository.Update(subscription);
        events.CollectEvent(new RenewalPaymentRetried(subscription.Id));
        return null;
    }

    private async Task<string?> ApplySuccessfulPaymentMethodAuthorizationAsync(
        Subscription subscription,
        VerifiedPaystackTransactionResult verified,
        IPaystackClient paystackClient,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var refund = await paystackClient.CreateRefundAsync(verified.Reference, verified.Amount, verified.Currency, cancellationToken);
        if (refund is null)
        {
            return "Failed to refund Paystack payment method authorization charge.";
        }

        subscription.SetPaystackAuthorization(verified.Authorization!.AuthorizationCode, verified.Authorization.Email, verified.Authorization.Signature, verified.PaymentMethod);
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), refund.Amount, refund.Amount, 0m, refund.Currency, PaymentTransactionStatus.Refunded, now, null, null, null)
            ]
        );

        subscriptionRepository.Update(subscription);
        events.CollectEvent(new PaymentMethodUpdated(subscription.Id));
        return null;
    }

    private async Task<int> AppendBillingEventAsync(
        Subscription subscription,
        PaystackPaymentAttempt paymentAttempt,
        VerifiedPaystackTransactionResult verified,
        SubscriptionPlan previousPlan,
        decimal? previousPriceAmount,
        DateTimeOffset now,
        HashSet<string> existingBillingEventIds,
        CancellationToken cancellationToken
    )
    {
        var providerEventId = $"paystack:{paymentAttempt.PaystackReference}:{paymentAttempt.Purpose}";
        if (existingBillingEventIds.Contains(providerEventId)) return 0;

        var eventType = GetBillingEventType(paymentAttempt.Purpose, previousPlan, paymentAttempt.Plan);
        var previousAmount = eventType is BillingEventType.SubscriptionCreated ? null : previousPriceAmount;
        var newAmount = eventType is BillingEventType.PaymentMethodUpdated or BillingEventType.PaymentRecovered ? null : subscription.CurrentPriceAmount;
        decimal? amountDelta = previousAmount is not null && newAmount is not null
            ? newAmount.Value - previousAmount.Value
            : eventType is BillingEventType.SubscriptionCreated
                ? verified.Amount
                : null;
        var committedMrr = subscription.CancelAtPeriodEnd ? 0m : subscription.CurrentPriceAmount ?? 0m;
        var billingEvent = BillingEvent.Create(
            subscription.TenantId,
            subscription.Id,
            providerEventId,
            eventType,
            now,
            committedMrr,
            eventType is BillingEventType.SubscriptionCreated ? null : previousPlan,
            paymentAttempt.Plan,
            previousAmount,
            newAmount,
            amountDelta,
            subscription.CurrentPriceCurrency ?? verified.Currency
        );

        await billingEventRepository.AddAsync(billingEvent, cancellationToken);
        existingBillingEventIds.Add(providerEventId);
        if (eventType is BillingEventType.SubscriptionCreated)
        {
            subscription.AdvanceSubscribedSinceBackwardFromBillingEvent(now);
            subscriptionRepository.Update(subscription);
        }

        return 1;
    }

    private static BillingEventType GetBillingEventType(PaystackPaymentPurpose purpose, SubscriptionPlan previousPlan, SubscriptionPlan? plan)
    {
        return purpose switch
        {
            PaystackPaymentPurpose.Subscribe when previousPlan == SubscriptionPlan.Basis => BillingEventType.SubscriptionCreated,
            PaystackPaymentPurpose.Upgrade when plan is not null && plan.Value.IsUpgradeFrom(previousPlan) => BillingEventType.SubscriptionUpgraded,
            PaystackPaymentPurpose.Renewal => BillingEventType.SubscriptionRenewed,
            PaystackPaymentPurpose.Retry => BillingEventType.PaymentRecovered,
            PaystackPaymentPurpose.PaymentMethodAuthorization => BillingEventType.PaymentMethodUpdated,
            _ => BillingEventType.NoOp
        };
    }

    private void MarkPaymentAttemptFailed(PaystackPaymentAttempt paymentAttempt, DateTimeOffset now, string error)
    {
        paymentAttempt.MarkFailed(now, error);
        paystackPaymentAttemptRepository.Update(paymentAttempt);
    }

    private void MarkEventFailed(PaystackEvent pendingEvent, DateTimeOffset now, string error)
    {
        pendingEvent.MarkFailed(now, error);
        paystackEventRepository.Update(pendingEvent);
    }

    private void MarkEventProcessed(PaystackEvent pendingEvent, Subscription subscription, DateTimeOffset now)
    {
        pendingEvent.SetPaystackAuthorizationCode(subscription.PaystackAuthorizationCode);
        pendingEvent.SetTenantId(subscription.TenantId);
        pendingEvent.MarkProcessed(now);
        paystackEventRepository.Update(pendingEvent);
    }

    private void SendTelemetryEvents(Tenant tenant, Subscription subscription)
    {
        TenantScopedTelemetryContext.Set(tenant.Id, subscription.Plan.ToString());

        while (events.HasEvents)
        {
            var telemetryEvent = events.Dequeue();
            telemetryClient.TrackEvent(telemetryEvent.GetType().Name, telemetryEvent.Properties);
            logger.LogInformation("Telemetry: {EventName} {EventProperties}", telemetryEvent.GetType().Name, string.Join(", ", telemetryEvent.Properties.Select(p => $"{p.Key}={p.Value}")));
        }
    }
}

public sealed record PaystackReconciliationResult(int BillingEventsAppended, int RecoveredPaymentAttempts)
{
    public static PaystackReconciliationResult Empty { get; } = new(0, 0);
}

internal sealed record PaystackAttemptProcessingResult(string? Reference, int BillingEventsAppended, bool PaymentAttemptRecovered)
{
    public static PaystackAttemptProcessingResult Empty { get; } = new(null, 0, false);
}

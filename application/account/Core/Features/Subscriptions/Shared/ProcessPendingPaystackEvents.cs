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
        var pendingEvents = await paystackEventRepository.GetPendingByPaystackCustomerIdWithLockAsync(paystackCustomerId, cancellationToken);

        if (pendingEvents.Length > 0)
        {
            var paystackClient = paystackClientFactory.GetClient();
            foreach (var pendingEvent in pendingEvents)
            {
                await ProcessPendingEventAsync(tenant, subscription, pendingEvent, paystackClient, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        SendTelemetryEvents(tenant, subscription);
    }

    private async Task ProcessPendingEventAsync(Tenant tenant, Subscription subscription, PaystackEvent pendingEvent, IPaystackClient paystackClient, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        pendingEvent.SetTenantId(subscription.TenantId);
        pendingEvent.SetPaystackAuthorizationCode(subscription.PaystackAuthorizationCode);

        if (pendingEvent.PaystackReference is null)
        {
            MarkEventFailed(pendingEvent, now, "Paystack webhook did not include a transaction reference.");
            return;
        }

        var paymentAttempt = await paystackPaymentAttemptRepository.GetByReferenceWithLockUnfilteredAsync(pendingEvent.PaystackReference, cancellationToken);
        if (paymentAttempt is null)
        {
            MarkEventFailed(pendingEvent, now, "Paystack payment attempt was not found.");
            return;
        }

        if (paymentAttempt.SubscriptionId != subscription.Id || paymentAttempt.PaystackCustomerId != subscription.PaystackCustomerId)
        {
            MarkEventFailed(pendingEvent, now, "Paystack payment attempt does not match this subscription.");
            return;
        }

        if (paymentAttempt.Status != PaystackPaymentAttemptStatus.Pending)
        {
            MarkEventProcessed(pendingEvent, subscription, now);
            return;
        }

        var verified = await paystackClient.VerifyTransactionAsync(paymentAttempt.PaystackReference, paymentAttempt.Purpose, cancellationToken);
        var validationError = ValidateVerifiedTransaction(subscription, paymentAttempt, verified);
        if (validationError is not null)
        {
            MarkPaymentAttemptFailed(paymentAttempt, pendingEvent, now, validationError);
            return;
        }

        var processingError = await ApplySuccessfulPaymentAsync(tenant, subscription, paymentAttempt, verified!, paystackClient, now, cancellationToken);
        if (processingError is not null)
        {
            MarkPaymentAttemptFailed(paymentAttempt, pendingEvent, now, processingError);
            return;
        }

        paymentAttempt.MarkSucceeded(now);
        paystackPaymentAttemptRepository.Update(paymentAttempt);
        MarkEventProcessed(pendingEvent, subscription, now);
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

    private void MarkPaymentAttemptFailed(PaystackPaymentAttempt paymentAttempt, PaystackEvent pendingEvent, DateTimeOffset now, string error)
    {
        paymentAttempt.MarkFailed(now, error);
        paystackPaymentAttemptRepository.Update(paymentAttempt);
        MarkEventFailed(pendingEvent, now, error);
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

using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Integrations.Paystack;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Shared;

public sealed class ProcessSubscriptionBilling(
    AccountDbContext dbContext,
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    PaystackClientFactory paystackClientFactory,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events,
    ILogger<ProcessSubscriptionBilling> logger
)
{
    private const int FailedRenewalGracePeriodDays = 7;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dueSubscriptions = await subscriptionRepository.GetDueForBillingUnfilteredAsync(now, cancellationToken);
        if (dueSubscriptions.Length == 0)
        {
            return;
        }

        var paystackClient = paystackClientFactory.GetClient();
        var priceCatalog = await paystackClient.GetPriceCatalogAsync(cancellationToken);

        foreach (var subscription in dueSubscriptions)
        {
            await ProcessDueSubscriptionAsync(subscription, priceCatalog, paystackClient, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessDueSubscriptionAsync(Subscription subscription, PriceCatalogItem[] priceCatalog, IPaystackClient paystackClient, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
        if (tenant is null)
        {
            logger.LogWarning("Tenant '{TenantId}' was not found while processing subscription billing for subscription '{SubscriptionId}'", subscription.TenantId, subscription.Id);
            return;
        }

        if (subscription is { CancelAtPeriodEnd: true, CurrentPeriodEnd: not null } && subscription.CurrentPeriodEnd <= now)
        {
            ExpireCancelledSubscription(subscription, tenant);
            return;
        }

        if (subscription.FirstPaymentFailedAt is not null)
        {
            if (subscription.FirstPaymentFailedAt.Value.AddDays(FailedRenewalGracePeriodDays) <= now)
            {
                SuspendFailedSubscription(subscription, tenant, now);
            }

            return;
        }

        var renewalPlan = subscription.ScheduledPlan ?? subscription.Plan;
        var catalogItem = priceCatalog.SingleOrDefault(item => item.Plan == renewalPlan);
        if (catalogItem is null)
        {
            logger.LogWarning("No Paystack catalog item found for renewal plan '{Plan}' on subscription '{SubscriptionId}'", renewalPlan, subscription.Id);
            return;
        }

        var billingEmail = subscription.PaystackAuthorizationEmail ?? subscription.BillingInfo?.Email;
        if (billingEmail is null)
        {
            logger.LogWarning("No billing email found for subscription '{SubscriptionId}' while processing renewal", subscription.Id);
            return;
        }

        var charge = await paystackClient.ChargeAuthorizationAsync(
            subscription.PaystackCustomerId!,
            subscription.PaystackSubscriptionId!,
            billingEmail,
            PaystackPaymentPurpose.Renewal,
            renewalPlan,
            catalogItem.UnitAmount,
            catalogItem.Currency,
            cancellationToken
        );

        if (charge is null)
        {
            logger.LogWarning("Paystack did not return a renewal charge result for subscription '{SubscriptionId}'", subscription.Id);
            return;
        }

        var paymentAttempt = PaystackPaymentAttempt.Create(
            subscription.TenantId,
            subscription.Id,
            charge.Reference,
            subscription.PaystackCustomerId!,
            subscription.PaystackSubscriptionId,
            PaystackPaymentPurpose.Renewal,
            renewalPlan,
            charge.Amount,
            charge.Currency
        );

        if (charge.Paid)
        {
            ApplySuccessfulRenewal(subscription, tenant, paymentAttempt, charge, renewalPlan, now);
        }
        else
        {
            ApplyFailedRenewal(subscription, paymentAttempt, charge, now);
        }

        await paystackPaymentAttemptRepository.AddAsync(paymentAttempt, cancellationToken);
    }

    private void ApplySuccessfulRenewal(
        Subscription subscription,
        Tenant tenant,
        PaystackPaymentAttempt paymentAttempt,
        AuthorizationChargeResult charge,
        SubscriptionPlan renewalPlan,
        DateTimeOffset now
    )
    {
        var nextBillingAt = now.AddMonths(1);
        subscription.StartBillingPeriod(renewalPlan, charge.Amount, charge.Currency, now, nextBillingAt, charge.PaymentMethod);
        subscription.ClearPaymentFailure();
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), charge.Amount, charge.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );

        tenant.UpdatePlan(renewalPlan);
        tenant.Activate();
        tenantRepository.Update(tenant);
        subscriptionRepository.Update(subscription);
        paymentAttempt.MarkSucceeded(now);

        events.CollectEvent(new SubscriptionRenewed(subscription.Id, renewalPlan, charge.Amount, charge.Amount, charge.Currency));
    }

    private void ApplyFailedRenewal(Subscription subscription, PaystackPaymentAttempt paymentAttempt, AuthorizationChargeResult charge, DateTimeOffset now)
    {
        subscription.SetPaymentFailed(now);
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), charge.Amount, charge.Currency, PaymentTransactionStatus.Failed, now, charge.ErrorMessage, null, null)
            ]
        );

        subscriptionRepository.Update(subscription);
        paymentAttempt.MarkFailed(now, charge.ErrorMessage ?? "Paystack could not charge the saved payment method.");

        events.CollectEvent(new PaymentFailed(subscription.Id, subscription.Plan, subscription.CurrentPriceAmount!.Value, subscription.CurrentPriceCurrency!));
    }

    private void ExpireCancelledSubscription(Subscription subscription, Tenant tenant)
    {
        var previousPlan = subscription.Plan;
        var previousPriceAmount = subscription.CurrentPriceAmount;
        var previousPriceCurrency = subscription.CurrentPriceCurrency;

        subscription.ResetToFreePlan();
        tenant.UpdatePlan(SubscriptionPlan.Basis);
        tenantRepository.Update(tenant);
        subscriptionRepository.Update(subscription);

        if (previousPriceAmount is not null && previousPriceCurrency is not null)
        {
            events.CollectEvent(new SubscriptionExpired(subscription.Id, previousPlan, 0, previousPriceAmount.Value, -previousPriceAmount.Value, previousPriceCurrency));
        }
    }

    private void SuspendFailedSubscription(Subscription subscription, Tenant tenant, DateTimeOffset now)
    {
        var previousPlan = subscription.Plan;
        var previousPriceAmount = subscription.CurrentPriceAmount;
        var previousPriceCurrency = subscription.CurrentPriceCurrency;

        subscription.ResetToFreePlan();
        tenant.UpdatePlan(SubscriptionPlan.Basis);
        tenant.Suspend(SuspensionReason.PaymentFailed, now);
        tenantRepository.Update(tenant);
        subscriptionRepository.Update(subscription);

        if (previousPriceAmount is not null && previousPriceCurrency is not null)
        {
            events.CollectEvent(new SubscriptionSuspended(subscription.Id, previousPlan, SuspensionReason.PaymentFailed, previousPriceAmount.Value, -previousPriceAmount.Value, previousPriceCurrency));
        }
    }
}

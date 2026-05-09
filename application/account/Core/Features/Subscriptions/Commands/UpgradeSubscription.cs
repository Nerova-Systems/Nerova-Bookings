using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record UpgradeSubscriptionCommand(SubscriptionPlan NewPlan) : ICommand, IRequest<Result<UpgradeSubscriptionResponse>>;

[PublicAPI]
public sealed record UpgradeSubscriptionResponse(
    string? AccessCode,
    string? Reference,
    string? PublicKey,
    decimal? Amount,
    string? Currency,
    string OperationPurpose
);

public sealed class UpgradeSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    ITenantRepository tenantRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events,
    ILogger<UpgradeSubscriptionHandler> logger
) : IRequestHandler<UpgradeSubscriptionCommand, Result<UpgradeSubscriptionResponse>>
{
    public async Task<Result<UpgradeSubscriptionResponse>> Handle(UpgradeSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<UpgradeSubscriptionResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (!command.NewPlan.IsUpgradeFrom(subscription.Plan))
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest($"Cannot upgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be higher.");
        }

        if (subscription.PaystackCustomerId is null)
        {
            logger.LogWarning("No Paystack customer found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<UpgradeSubscriptionResponse>.BadRequest("No Paystack customer found.");
        }

        if (subscription.PaystackSubscriptionId is null)
        {
            logger.LogWarning("No Paystack authorization found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<UpgradeSubscriptionResponse>.BadRequest("No active Paystack authorization found.");
        }

        var billingEmail = subscription.PaystackAuthorizationEmail ?? subscription.BillingInfo?.Email;
        if (billingEmail is null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest("Billing information must include an email before upgrading.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var priceCatalog = await paystackClient.GetPriceCatalogAsync(cancellationToken);
        var targetPlanPrice = priceCatalog.SingleOrDefault(p => p.Plan == command.NewPlan);
        if (targetPlanPrice is null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest("Could not retrieve upgrade price.");
        }

        var now = timeProvider.GetUtcNow();
        var proratedAmount = SubscriptionBillingCalculator.CalculateProratedUpgradeAmount(
            subscription.CurrentPriceAmount ?? 0m,
            targetPlanPrice.UnitAmount,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            now
        );

        var charge = await paystackClient.ChargeAuthorizationAsync(
            subscription.PaystackCustomerId,
            subscription.PaystackSubscriptionId,
            billingEmail,
            PaystackPaymentPurpose.Upgrade,
            command.NewPlan,
            proratedAmount,
            targetPlanPrice.Currency,
            cancellationToken
        );
        if (charge is null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest("Failed to upgrade subscription in Paystack.");
        }

        var paymentAttempt = PaystackPaymentAttempt.Create(
            subscription.TenantId,
            subscription.Id,
            charge.Reference,
            subscription.PaystackCustomerId,
            subscription.PaystackSubscriptionId,
            PaystackPaymentPurpose.Upgrade,
            command.NewPlan,
            charge.Amount,
            charge.Currency
        );

        if (!charge.Paid)
        {
            paymentAttempt.MarkFailed(now, charge.ErrorMessage ?? "Paystack could not charge the saved payment method.");
            await paystackPaymentAttemptRepository.AddAsync(paymentAttempt, cancellationToken);
            return Result<UpgradeSubscriptionResponse>.BadRequest(charge.ErrorMessage ?? "Paystack could not charge the saved payment method.");
        }

        var previousPlan = subscription.Plan;
        var previousPriceAmount = subscription.CurrentPriceAmount ?? 0m;
        var currentPeriodStart = subscription.CurrentPeriodStart ?? now;
        var currentPeriodEnd = subscription.CurrentPeriodEnd ?? now.AddMonths(1);
        subscription.StartBillingPeriod(command.NewPlan, targetPlanPrice.UnitAmount, charge.Currency, currentPeriodStart, currentPeriodEnd, charge.PaymentMethod);
        subscription.SetPaymentTransactions([
                .. subscription.PaymentTransactions,
                new PaymentTransaction(PaymentTransactionId.NewId(), charge.Amount, charge.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );

        var tenant = await tenantRepository.GetByIdUnfilteredAsync(subscription.TenantId, cancellationToken);
        if (tenant is not null)
        {
            tenant.UpdatePlan(command.NewPlan);
            tenant.Activate();
            tenantRepository.Update(tenant);
        }

        subscriptionRepository.Update(subscription);
        paymentAttempt.MarkSucceeded(now);
        await paystackPaymentAttemptRepository.AddAsync(paymentAttempt, cancellationToken);
        events.CollectEvent(new SubscriptionUpgraded(subscription.Id, previousPlan, command.NewPlan, 0, previousPriceAmount, targetPlanPrice.UnitAmount, targetPlanPrice.UnitAmount - previousPriceAmount, charge.Currency));

        return new UpgradeSubscriptionResponse(null, null, null, charge.Amount, charge.Currency, nameof(PaystackPaymentPurpose.Upgrade));
    }
}

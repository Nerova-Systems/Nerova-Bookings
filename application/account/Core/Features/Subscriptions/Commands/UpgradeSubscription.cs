using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record UpgradeSubscriptionCommand(SubscriptionPlan NewPlan) : ICommand, IRequest<Result>;

public sealed class UpgradeSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    IPayFastClient payFastClient,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<UpgradeSubscriptionCommand, Result>
{
    public async Task<Result> Handle(UpgradeSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status != SubscriptionStatus.Active)
        {
            return Result.BadRequest("Subscription must be active to upgrade.");
        }

        if (!command.NewPlan.IsUpgradeFrom(subscription.Plan))
        {
            return Result.BadRequest($"Cannot upgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be higher.");
        }

        if (subscription.PayFastToken is null)
        {
            return Result.BadRequest("No payment method on file. Please contact support.");
        }

        var now = timeProvider.GetUtcNow();
        var currentPrice = SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan);
        var newPrice = SubscriptionPlanPricing.GetMonthlyPrice(command.NewPlan);

        var daysInPeriod = 30m;
        var daysRemaining = subscription.CurrentPeriodEnd.HasValue ? (decimal)(subscription.CurrentPeriodEnd.Value - now).TotalDays : daysInPeriod;
        daysRemaining = Math.Max(0, Math.Min(daysRemaining, daysInPeriod));

        var proratedCharge = Math.Round((newPrice - currentPrice) * (daysRemaining / daysInPeriod), 2);

        if (proratedCharge > 0)
        {
            var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken, proratedCharge, $"Nerova Bookings upgrade to {command.NewPlan}", cancellationToken);
            if (!charged)
            {
                return Result.BadRequest("Payment failed. Please check your payment method and try again.");
            }

            var transaction = new PaymentTransaction(
                PaymentTransactionId.NewId(),
                proratedCharge,
                SubscriptionPlanPricing.Currency,
                PaymentTransactionStatus.Succeeded,
                now,
                null,
                null,
                null
            );
            subscription.SetPaymentTransactions(subscription.PaymentTransactions.Add(transaction));
        }

        var fromPlan = subscription.Plan;
        var daysOnCurrentPlan = subscription.CurrentPeriodStart.HasValue ? (int)(now - subscription.CurrentPeriodStart.Value).TotalDays : 0;
        var mrrImpact = newPrice - currentPrice;

        subscription.SetPlan(command.NewPlan);
        subscription.SetScheduledPlan(null);

        events.CollectEvent(new SubscriptionUpgraded(subscription.Id, fromPlan, command.NewPlan, daysOnCurrentPlan, currentPrice, newPrice, mrrImpact, SubscriptionPlanPricing.Currency));

        subscriptionRepository.Update(subscription);
        return Result.Success();
    }
}

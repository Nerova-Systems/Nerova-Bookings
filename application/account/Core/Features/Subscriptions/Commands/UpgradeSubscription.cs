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
    TimeProvider timeProvider,
    ILogger<UpgradeSubscriptionHandler> logger
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
        var fromPlan = subscription.Plan;
        var currentPrice = SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan);
        var newPrice = SubscriptionPlanPricing.GetMonthlyPrice(command.NewPlan);

        // Proration: count whole days remaining, charge the per-day price difference for those days.
        // Working in whole days (Math.Ceiling rounds partial days up so the user is fairly billed for
        // the day the upgrade happens) and rounding the cents result with banker's rounding keeps the
        // amount stable and prevents off-by-cents drift across upgrades.
        const int daysInPeriod = 30;
        var rawDaysRemaining = subscription.CurrentPeriodEnd.HasValue
            ? (subscription.CurrentPeriodEnd.Value - now).TotalDays
            : daysInPeriod;
        var daysRemaining = Math.Clamp((int)Math.Ceiling(rawDaysRemaining), 0, daysInPeriod);

        var priceDifference = newPrice - currentPrice;
        var proratedCharge = Math.Round(priceDifference * daysRemaining / daysInPeriod, 2, MidpointRounding.ToEven);
        var daysOnCurrentPlan = subscription.CurrentPeriodStart.HasValue ? (int)(now - subscription.CurrentPeriodStart.Value).TotalDays : 0;
        var mrrImpact = priceDifference;

        logger.LogInformation(
            "Upgrade {SubscriptionId}: from {FromPlan} (R{CurrentPrice}) to {NewPlan} (R{NewPrice}); CurrentPeriodEnd={PeriodEnd}, daysRemaining={DaysRemaining}, proratedCharge=R{ProratedCharge}, hasToken={HasToken}",
            subscription.Id,
            subscription.Plan,
            currentPrice,
            command.NewPlan,
            newPrice,
            subscription.CurrentPeriodEnd,
            daysRemaining,
            proratedCharge,
            subscription.PayFastToken is not null);

        if (proratedCharge > 0)
        {
            logger.LogInformation("Calling PayFast adhoc charge for upgrade {SubscriptionId}: R{Amount}", subscription.Id, proratedCharge);
            var charged = await payFastClient.ChargeTokenAsync(subscription.PayFastToken, proratedCharge, $"Nerova Bookings upgrade to {command.NewPlan}", cancellationToken);
            if (!charged)
            {
                logger.LogWarning("PayFast adhoc charge returned false for upgrade {SubscriptionId}; aborting upgrade", subscription.Id);
                return Result.BadRequest("Payment failed. Please check your payment method and try again.");
            }
            logger.LogInformation("PayFast adhoc charge SUCCEEDED for upgrade {SubscriptionId}: R{Amount}", subscription.Id, proratedCharge);

            // PayFast fires an ITN webhook for the adhoc charge that runs concurrently with this handler
            // and writes its own changes to the subscription (RenewBillingPeriod + payment transaction).
            // Reload to pick up that fresh state — without this we'd hit a concurrency conflict on
            // ModifiedAt when saving. The ITN owns the payment transaction record; we only own the plan
            // change.
            subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);
        }
        else
        {
            logger.LogWarning(
                "Upgrade {SubscriptionId}: proratedCharge is R{ProratedCharge} (<=0); SKIPPING PayFast charge but still applying plan change. This may indicate a stale CurrentPeriodEnd or zero days remaining.",
                subscription.Id,
                proratedCharge);
        }

        subscription.SetPlan(command.NewPlan);
        subscription.SetScheduledPlan(null);

        events.CollectEvent(new SubscriptionUpgraded(subscription.Id, fromPlan, command.NewPlan, daysOnCurrentPlan, currentPrice, newPrice, mrrImpact, SubscriptionPlanPricing.Currency));

        subscriptionRepository.Update(subscription);
        return Result.Success();
    }
}

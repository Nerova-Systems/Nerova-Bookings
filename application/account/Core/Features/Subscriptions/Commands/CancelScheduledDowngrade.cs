using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record CancelScheduledDowngradeCommand : ICommand, IRequest<Result>;

public sealed class CancelScheduledDowngradeHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<CancelScheduledDowngradeCommand, Result>
{
    public async Task<Result> Handle(CancelScheduledDowngradeCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.ScheduledPlan is null)
        {
            return Result.BadRequest("No scheduled downgrade to cancel.");
        }

        var now = timeProvider.GetUtcNow();
        var currentPrice = SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan);
        var scheduledPrice = SubscriptionPlanPricing.GetMonthlyPrice(subscription.ScheduledPlan.Value);
        var daysUntilDowngrade = subscription.CurrentPeriodEnd.HasValue ? (int?)(int)(subscription.CurrentPeriodEnd.Value - now).TotalDays : null;
        var daysSinceScheduled = 0;
        var mrrImpact = currentPrice - scheduledPrice;

        var scheduledPlan = subscription.ScheduledPlan.Value;
        subscription.SetScheduledPlan(null);

        events.CollectEvent(new SubscriptionDowngradeCancelled(subscription.Id, subscription.Plan, scheduledPlan, daysUntilDowngrade, daysSinceScheduled, currentPrice, mrrImpact, SubscriptionPlanPricing.Currency));

        subscriptionRepository.Update(subscription);
        return Result.Success();
    }
}

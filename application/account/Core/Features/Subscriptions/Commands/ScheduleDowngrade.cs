using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ScheduleDowngradeCommand(SubscriptionPlan NewPlan) : ICommand, IRequest<Result>;

public sealed class ScheduleDowngradeHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<ScheduleDowngradeCommand, Result>
{
    public async Task<Result> Handle(ScheduleDowngradeCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.Status != SubscriptionStatus.Active)
        {
            return Result.BadRequest("Subscription must be active to schedule a downgrade.");
        }

        if (!command.NewPlan.IsDowngradeFrom(subscription.Plan))
        {
            return Result.BadRequest($"Cannot downgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be lower.");
        }

        if (command.NewPlan == SubscriptionPlan.Trial)
        {
            return Result.BadRequest("Cannot downgrade to the Trial plan.");
        }

        var now = timeProvider.GetUtcNow();
        var currentPrice = SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan);
        var newPrice = SubscriptionPlanPricing.GetMonthlyPrice(command.NewPlan);
        var daysUntilDowngrade = subscription.CurrentPeriodEnd.HasValue ? (int?)(int)(subscription.CurrentPeriodEnd.Value - now).TotalDays : null;
        var mrrImpact = newPrice - currentPrice;

        subscription.SetScheduledPlan(command.NewPlan);

        events.CollectEvent(new SubscriptionDowngradeScheduled(subscription.Id, subscription.Plan, command.NewPlan, daysUntilDowngrade, currentPrice, mrrImpact, SubscriptionPlanPricing.Currency));

        subscriptionRepository.Update(subscription);
        return Result.Success();
    }
}

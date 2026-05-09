using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ScheduleDowngradeCommand(SubscriptionPlan NewPlan) : ICommand, IRequest<Result>;

public sealed class ScheduleDowngradeHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    ILogger<ScheduleDowngradeHandler> logger
) : IRequestHandler<ScheduleDowngradeCommand, Result>
{
    public async Task<Result> Handle(ScheduleDowngradeCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackAuthorizationCode is null)
        {
            logger.LogWarning("No Paystack authorization found for subscription '{SubscriptionId}'", subscription.Id);
            return Result.BadRequest("No active Paystack authorization found.");
        }

        if (!command.NewPlan.IsDowngradeFrom(subscription.Plan))
        {
            return Result.BadRequest($"Cannot downgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be lower.");
        }

        if (command.NewPlan == SubscriptionPlan.Basis)
        {
            return Result.BadRequest("Cannot downgrade to the Basis plan.");
        }

        subscription.SetScheduledPlan(command.NewPlan);
        subscriptionRepository.Update(subscription);

        return Result.Success();
    }
}

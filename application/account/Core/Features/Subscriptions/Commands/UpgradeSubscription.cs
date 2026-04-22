using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record UpgradeSubscriptionCommand(SubscriptionPlan NewPlan) : ICommand, IRequest<Result>;

public sealed class UpgradeSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext
) : IRequestHandler<UpgradeSubscriptionCommand, Result>
{
    public async Task<Result> Handle(UpgradeSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (!command.NewPlan.IsUpgradeFrom(subscription.Plan))
        {
            return Result.BadRequest($"Cannot upgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be higher.");
        }

        // TODO: Implement PayFast upgrade in pf-03

        return Result.Success();
    }
}

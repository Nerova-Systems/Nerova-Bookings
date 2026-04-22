using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record CancelScheduledDowngradeCommand : ICommand, IRequest<Result>;

public sealed class CancelScheduledDowngradeHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext
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

        // TODO: Implement PayFast cancel scheduled downgrade in pf-03

        return Result.Success();
    }
}

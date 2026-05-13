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
    IExecutionContext executionContext,
    ILogger<CancelScheduledDowngradeHandler> logger
) : IRequestHandler<CancelScheduledDowngradeCommand, Result>
{
    public async Task<Result> Handle(CancelScheduledDowngradeCommand command, CancellationToken cancellationToken)
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

        if (subscription.ScheduledPlan is null)
        {
            return Result.BadRequest("No scheduled downgrade to cancel.");
        }

        subscription.SetScheduledPlan(null, null);
        subscriptionRepository.Update(subscription);

        return Result.Success();
    }
}

using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ReactivateSubscriptionCommand : ICommand, IRequest<Result<ReactivateSubscriptionResponse>>;

[PublicAPI]
public sealed record ReactivateSubscriptionResponse;

public sealed class ReactivateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IExecutionContext executionContext,
    ILogger<ReactivateSubscriptionHandler> logger
) : IRequestHandler<ReactivateSubscriptionCommand, Result<ReactivateSubscriptionResponse>>
{
    public async Task<Result<ReactivateSubscriptionResponse>> Handle(ReactivateSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<ReactivateSubscriptionResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (!subscription.CancelAtPeriodEnd)
        {
            return Result<ReactivateSubscriptionResponse>.BadRequest("Subscription is not cancelled. Nothing to reactivate.");
        }

        if (subscription.PaystackSubscriptionId is null)
        {
            logger.LogWarning("No Paystack authorization found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<ReactivateSubscriptionResponse>.BadRequest("No active Paystack authorization found.");
        }

        subscription.SetCancellation(false, null, null);
        subscriptionRepository.Update(subscription);

        return new ReactivateSubscriptionResponse();
    }
}

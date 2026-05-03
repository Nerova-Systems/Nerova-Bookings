using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record CancelScheduledDowngradeCommand : ICommand, IRequest<Result>;

public sealed class CancelScheduledDowngradeHandler(
    ISubscriptionRepository subscriptionRepository,
    PaystackClientFactory paystackClientFactory,
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

        if (subscription.PaystackSubscriptionId is null)
        {
            logger.LogWarning("No Paystack subscription found for subscription '{SubscriptionId}'", subscription.Id);
            return Result.BadRequest("No active Paystack subscription found.");
        }

        if (subscription.ScheduledPlan is null)
        {
            return Result.BadRequest("No scheduled downgrade to cancel.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var success = await paystackClient.CancelScheduledDowngradeAsync(subscription.PaystackSubscriptionId, cancellationToken);
        if (!success)
        {
            return Result.BadRequest("Failed to cancel scheduled downgrade in Paystack.");
        }

        // Subscription is updated and telemetry is collected in ProcessPendingPaystackEvents when Paystack confirms the state change via webhook

        return Result.Success();
    }
}

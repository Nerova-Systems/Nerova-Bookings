using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ReactivateSubscriptionCommand : ICommand, IRequest<Result<ReactivateSubscriptionResponse>>;

[PublicAPI]
public sealed record ReactivateSubscriptionResponse(string? AuthorizationUrl, string? Reference);

public sealed class ReactivateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    PaystackClientFactory paystackClientFactory,
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
            logger.LogWarning("No Paystack subscription found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<ReactivateSubscriptionResponse>.BadRequest("No active Paystack subscription found.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var reactivateSuccess = await paystackClient.ReactivateSubscriptionAsync(subscription.PaystackSubscriptionId, cancellationToken);
        if (!reactivateSuccess)
        {
            return Result<ReactivateSubscriptionResponse>.BadRequest("Failed to reactivate subscription in Paystack.");
        }

        // Subscription is updated and telemetry is collected in ProcessPendingPaystackEvents when Paystack confirms the state change via webhook

        return new ReactivateSubscriptionResponse(null, null);
    }
}

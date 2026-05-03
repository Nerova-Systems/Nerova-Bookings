using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record UpgradeSubscriptionCommand(SubscriptionPlan NewPlan) : ICommand, IRequest<Result<UpgradeSubscriptionResponse>>;

[PublicAPI]
public sealed record UpgradeSubscriptionResponse(string? AuthorizationUrl, string? Reference);

public sealed class UpgradeSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ILogger<UpgradeSubscriptionHandler> logger
) : IRequestHandler<UpgradeSubscriptionCommand, Result<UpgradeSubscriptionResponse>>
{
    public async Task<Result<UpgradeSubscriptionResponse>> Handle(UpgradeSubscriptionCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<UpgradeSubscriptionResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackSubscriptionId is null)
        {
            logger.LogWarning("No Paystack subscription found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<UpgradeSubscriptionResponse>.BadRequest("No active Paystack subscription found.");
        }

        if (!command.NewPlan.IsUpgradeFrom(subscription.Plan))
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest($"Cannot upgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be higher.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var upgradeResult = await paystackClient.UpgradeSubscriptionAsync(subscription.PaystackSubscriptionId, command.NewPlan, cancellationToken);
        if (upgradeResult is null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest("Failed to upgrade subscription in Paystack.");
        }

        if (upgradeResult.ErrorMessage is not null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest(upgradeResult.ErrorMessage);
        }

        // Subscription is updated and telemetry is collected in ProcessPendingPaystackEvents when Paystack confirms the state change via webhook

        return new UpgradeSubscriptionResponse(upgradeResult.AuthorizationUrl, upgradeResult.Reference);
    }
}

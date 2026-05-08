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
public sealed record UpgradeSubscriptionResponse(
    string? AccessCode,
    string? Reference,
    string? PublicKey,
    decimal? Amount,
    string? Currency,
    string OperationPurpose
);

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

        if (!command.NewPlan.IsUpgradeFrom(subscription.Plan))
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest($"Cannot upgrade from '{subscription.Plan}' to '{command.NewPlan}'. Target plan must be higher.");
        }

        if (subscription.PaystackCustomerId is null)
        {
            logger.LogWarning("No Paystack customer found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<UpgradeSubscriptionResponse>.BadRequest("No Paystack customer found.");
        }

        if (subscription.PaystackSubscriptionId is null)
        {
            logger.LogWarning("No Paystack subscription found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<UpgradeSubscriptionResponse>.BadRequest("No active Paystack subscription found.");
        }

        var billingEmail = subscription.PaystackAuthorizationEmail ?? subscription.BillingInfo?.Email;
        if (billingEmail is null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest("Billing information must include an email before upgrading.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var upgradeResult = await paystackClient.UpgradeSubscriptionAsync(subscription.PaystackCustomerId, subscription.PaystackSubscriptionId, billingEmail, command.NewPlan, cancellationToken);
        if (upgradeResult is null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest("Failed to upgrade subscription in Paystack.");
        }

        if (upgradeResult.ErrorMessage is not null)
        {
            return Result<UpgradeSubscriptionResponse>.BadRequest(upgradeResult.ErrorMessage);
        }

        // Subscription is updated and telemetry is collected when Paystack confirms the transaction.

        var publicKey = upgradeResult.AccessCode is not null ? paystackClientFactory.GetPublicKey() : null;
        return new UpgradeSubscriptionResponse(upgradeResult.AccessCode, upgradeResult.Reference, publicKey, upgradeResult.Amount, upgradeResult.Currency, nameof(PaystackPaymentPurpose.Upgrade));
    }
}

using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record StartPaymentMethodSetupCommand : ICommand, IRequest<Result<StartPaymentMethodSetupResponse>>;

[PublicAPI]
public sealed record StartPaymentMethodSetupResponse(
    string AccessCode,
    string Reference,
    string PublicKey,
    decimal Amount,
    string Currency,
    string OperationPurpose
);

public sealed class StartPaymentMethodSetupHandler(
    ISubscriptionRepository subscriptionRepository,
    IPaystackPaymentAttemptRepository paystackPaymentAttemptRepository,
    PaystackClientFactory paystackClientFactory,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    ILogger<StartPaymentMethodSetupHandler> logger
) : IRequestHandler<StartPaymentMethodSetupCommand, Result<StartPaymentMethodSetupResponse>>
{
    public async Task<Result<StartPaymentMethodSetupResponse>> Handle(StartPaymentMethodSetupCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<StartPaymentMethodSetupResponse>.Forbidden("Only owners can manage subscriptions.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackCustomerId is null)
        {
            logger.LogWarning("No Paystack customer found for subscription '{SubscriptionId}'", subscription.Id);
            return Result<StartPaymentMethodSetupResponse>.BadRequest("No Paystack customer found. A subscription must be created first.");
        }

        var publicKey = paystackClientFactory.GetPublicKey();
        if (publicKey is null)
        {
            logger.LogWarning("Paystack public key is not configured");
            return Result<StartPaymentMethodSetupResponse>.BadRequest("Paystack is not configured for payment method updates.");
        }

        if (subscription.BillingInfo?.Email is null)
        {
            return Result<StartPaymentMethodSetupResponse>.BadRequest("Billing information must include an email before updating a payment method.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var initialization = await paystackClient.CreatePaymentMethodAuthorizationAsync(subscription.PaystackCustomerId, subscription.BillingInfo.Email, cancellationToken);
        if (initialization is null)
        {
            return Result<StartPaymentMethodSetupResponse>.BadRequest("Failed to initialize payment method authorization.");
        }

        await paystackPaymentAttemptRepository.AddAsync(
            PaystackPaymentAttempt.Create(
                subscription.TenantId,
                subscription.Id,
                initialization.Reference,
                subscription.PaystackCustomerId,
                null,
                PaystackPaymentPurpose.PaymentMethodAuthorization,
                null,
                initialization.Amount,
                initialization.Currency
            ),
            cancellationToken
        );

        events.CollectEvent(new PaymentMethodSetupStarted(subscription.Id));

        return new StartPaymentMethodSetupResponse(initialization.AccessCode, initialization.Reference, publicKey, initialization.Amount, initialization.Currency, initialization.Purpose.ToString());
    }
}

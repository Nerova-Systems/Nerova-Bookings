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
public sealed record StartPaymentMethodSetupResponse(string UpdateUrl);

public sealed class StartPaymentMethodSetupHandler(
    ISubscriptionRepository subscriptionRepository,
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

        if (subscription.PaystackSubscriptionId is null)
        {
            return Result<StartPaymentMethodSetupResponse>.BadRequest("A subscription must be active before updating the payment method.");
        }

        var paystackClient = paystackClientFactory.GetClient();
        var updateUrl = await paystackClient.CreatePaymentMethodUpdateLinkAsync(subscription.PaystackSubscriptionId, cancellationToken);
        if (updateUrl is null)
        {
            return Result<StartPaymentMethodSetupResponse>.BadRequest("Failed to create payment method update link.");
        }

        events.CollectEvent(new PaymentMethodSetupStarted(subscription.Id));

        return new StartPaymentMethodSetupResponse(updateUrl);
    }
}

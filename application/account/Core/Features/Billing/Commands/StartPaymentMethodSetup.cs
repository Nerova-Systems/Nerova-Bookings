using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using Account.Integrations.PayFast;
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
    IExecutionContext executionContext,
    IPayFastClient payFastClient,
    ITelemetryEventsCollector events
) : IRequestHandler<StartPaymentMethodSetupCommand, Result<StartPaymentMethodSetupResponse>>
{
    public async Task<Result<StartPaymentMethodSetupResponse>> Handle(StartPaymentMethodSetupCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<StartPaymentMethodSetupResponse>.Forbidden("Only owners can update the payment method.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PayFastToken is null)
        {
            return Result<StartPaymentMethodSetupResponse>.BadRequest("No payment method on file. Subscribe first to register a card.");
        }

        // PayFast hosts a customer-facing page for updating the card associated with a recurring token.
        // We open this URL in a new tab; PayFast handles the card form and updates the token in place.
        // No webhook is fired for the update itself — the same token continues to work for adhoc charges.
        var updateUrl = payFastClient.GetUpdateCardUrl(subscription.PayFastToken);

        events.CollectEvent(new PaymentMethodSetupStarted(subscription.Id));

        return new StartPaymentMethodSetupResponse(updateUrl);
    }
}

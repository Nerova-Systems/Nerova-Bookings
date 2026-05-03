using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace Account.Features.Subscriptions.Commands;

/// <summary>
///     Phase 1 of two-phase webhook processing. Validates the Paystack signature, stores the event
///     as pending, and returns the customer ID so the API can trigger phase 2 processing.
/// </summary>
[PublicAPI]
public sealed record AcknowledgePaystackWebhookCommand(string Payload, string SignatureHeader) : ICommand, IRequest<Result<PaystackCustomerId?>>;

public sealed class AcknowledgePaystackWebhookHandler(
    IPaystackEventRepository paystackEventRepository,
    PaystackClientFactory paystackClientFactory,
    TimeProvider timeProvider
) : IRequestHandler<AcknowledgePaystackWebhookCommand, Result<PaystackCustomerId?>>
{
    public async Task<Result<PaystackCustomerId?>> Handle(AcknowledgePaystackWebhookCommand command, CancellationToken cancellationToken)
    {
        var paystackClient = paystackClientFactory.GetClient();
        var webhookEvent = paystackClient.VerifyWebhookSignature(command.Payload, command.SignatureHeader);
        if (webhookEvent is null)
        {
            return Result<PaystackCustomerId?>.BadRequest("Invalid webhook signature.");
        }

        if (await paystackEventRepository.ExistsAsync(webhookEvent.EventId, cancellationToken))
        {
            return Result<PaystackCustomerId?>.Success(webhookEvent.CustomerId);
        }

        var now = timeProvider.GetUtcNow();
        var customerId = webhookEvent.CustomerId;

        var paystackEvent = PaystackEvent.Create(webhookEvent.EventId, webhookEvent.EventType, customerId, command.Payload);

        if (customerId is null)
        {
            paystackEvent.MarkIgnored(now);
        }

        await paystackEventRepository.AddAsync(paystackEvent, cancellationToken);

        return customerId;
    }
}

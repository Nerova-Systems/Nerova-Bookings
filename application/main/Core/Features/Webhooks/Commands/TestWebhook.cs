using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Infrastructure;
using Main.Features.Webhooks.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Webhooks.Commands;

/// <summary>
///     Test-fire endpoint: enqueues a synthetic <see cref="WebhookEventType.Ping" /> delivery to
///     the target webhook so users can verify their endpoint receives + validates the signature.
///     The delivery flows through the same persistence + retry path as production events.
/// </summary>
[PublicAPI]
public sealed record TestWebhookCommand(WebhookId Id) : ICommand, IRequest<Result<TestWebhookResponse>>;

public sealed class TestWebhookHandler(
    IWebhookRepository webhookRepository,
    IWebhookDispatcher dispatcher,
    IExecutionContext executionContext,
    TimeProvider timeProvider
) : IRequestHandler<TestWebhookCommand, Result<TestWebhookResponse>>
{
    public async Task<Result<TestWebhookResponse>> Handle(TestWebhookCommand command, CancellationToken cancellationToken)
    {
        var gate = WebhookAccessGate.Evaluate<TestWebhookResponse>(executionContext);
        if (gate.Failure is { } failure) return failure;

        var webhook = await webhookRepository.GetByIdAsync(command.Id, cancellationToken);
        if (webhook is null) return Result<TestWebhookResponse>.NotFound($"Webhook '{command.Id}' was not found.");

        var payload = JsonSerializer.Serialize(new
        {
            triggerEvent = WebhookEventType.Ping.ToString(),
            createdAt = timeProvider.GetUtcNow(),
            webhookId = webhook.Id.Value,
            message = "Ping from Nerova webhook test-fire"
        });

        var deliveryId = await dispatcher.EnqueueAsync(webhook.Id, WebhookEventType.Ping, payload, cancellationToken);
        return new TestWebhookResponse(deliveryId);
    }
}

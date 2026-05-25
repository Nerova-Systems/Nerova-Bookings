using JetBrains.Annotations;
using Main.Features.Webhooks.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Webhooks.Commands;

[PublicAPI]
public sealed record DeleteWebhookCommand(WebhookId Id) : ICommand, IRequest<Result>;

public sealed class DeleteWebhookHandler(
    IWebhookRepository webhookRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteWebhookCommand, Result>
{
    public async Task<Result> Handle(DeleteWebhookCommand command, CancellationToken cancellationToken)
    {
        var gate = WebhookAccessGate.EvaluateNonGeneric(executionContext);
        if (gate.Failure is { } failure) return failure;

        var webhook = await webhookRepository.GetByIdAsync(command.Id, cancellationToken);
        if (webhook is null) return Result.NotFound($"Webhook '{command.Id}' was not found.");

        webhookRepository.Remove(webhook);
        events.CollectEvent(new WebhookDeleted(webhook.Id));
        return Result.Success();
    }
}

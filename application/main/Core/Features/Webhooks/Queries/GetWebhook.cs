using JetBrains.Annotations;
using Main.Features.Webhooks.Commands;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Webhooks.Queries;

[PublicAPI]
public sealed record GetWebhookQuery(WebhookId Id) : IRequest<Result<WebhookResponse>>;

public sealed class GetWebhookHandler(
    IWebhookRepository webhookRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetWebhookQuery, Result<WebhookResponse>>
{
    public async Task<Result<WebhookResponse>> Handle(GetWebhookQuery query, CancellationToken cancellationToken)
    {
        var gate = WebhookAccessGate.Evaluate<WebhookResponse>(executionContext);
        if (gate.Failure is { } failure) return failure;

        var webhook = await webhookRepository.GetByIdAsync(query.Id, cancellationToken);
        if (webhook is null) return Result<WebhookResponse>.NotFound($"Webhook '{query.Id}' was not found.");

        return WebhookResponse.From(webhook, false);
    }
}

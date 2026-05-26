using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Webhooks.Commands;

[PublicAPI]
public sealed record UpdateWebhookCommand(
    WebhookId Id,
    string TargetUrl,
    WebhookEventType[] EventSubscriptions,
    bool Active,
    bool RegenerateSecret
) : ICommand, IRequest<Result<WebhookResponse>>;

public sealed class UpdateWebhookValidator : AbstractValidator<UpdateWebhookCommand>
{
    public UpdateWebhookValidator()
    {
        RuleFor(c => c.TargetUrl).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.TargetUrl)
            .Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Target URL must be an absolute http(s) URL.");
        RuleFor(c => c.EventSubscriptions).NotEmpty().WithMessage("At least one event subscription is required.");
    }
}

public sealed class UpdateWebhookHandler(
    IWebhookRepository webhookRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateWebhookCommand, Result<WebhookResponse>>
{
    public async Task<Result<WebhookResponse>> Handle(UpdateWebhookCommand command, CancellationToken cancellationToken)
    {
        var gate = WebhookAccessGate.Evaluate<WebhookResponse>(executionContext);
        if (gate.Failure is { } failure) return failure;

        var webhook = await webhookRepository.GetByIdAsync(command.Id, cancellationToken);
        if (webhook is null) return Result<WebhookResponse>.NotFound($"Webhook '{command.Id}' was not found.");

        webhook.Update(command.TargetUrl, command.EventSubscriptions.Distinct().ToArray(), command.Active);
        if (command.RegenerateSecret) webhook.RegenerateSecret();
        webhookRepository.Update(webhook);
        events.CollectEvent(new WebhookUpdated(webhook.Id));
        return WebhookResponse.From(webhook, command.RegenerateSecret);
    }
}

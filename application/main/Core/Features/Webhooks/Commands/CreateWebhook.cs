using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Webhooks.Commands;

[PublicAPI]
public sealed record CreateWebhookCommand(
    string TargetUrl,
    WebhookEventType[] EventSubscriptions,
    bool Active,
    EventTypeId? EventTypeId
) : ICommand, IRequest<Result<WebhookResponse>>;

public sealed class CreateWebhookValidator : AbstractValidator<CreateWebhookCommand>
{
    public CreateWebhookValidator()
    {
        RuleFor(c => c.TargetUrl).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.TargetUrl).Must(BeAbsoluteUrl).WithMessage("Target URL must be an absolute http(s) URL.");
        RuleFor(c => c.EventSubscriptions).NotEmpty().WithMessage("At least one event subscription is required.");
    }

    private static bool BeAbsoluteUrl(string targetUrl)
    {
        return Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

public sealed class CreateWebhookHandler(
    IWebhookRepository webhookRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateWebhookCommand, Result<WebhookResponse>>
{
    public async Task<Result<WebhookResponse>> Handle(CreateWebhookCommand command, CancellationToken cancellationToken)
    {
        var gate = WebhookAccessGate.Evaluate<WebhookResponse>(executionContext);
        if (gate.Failure is { } failure) return failure;

        var webhook = Webhook.Create(
            gate.TenantId,
            gate.UserId,
            command.EventTypeId,
            command.TargetUrl,
            command.EventSubscriptions.Distinct().ToArray(),
            command.Active
        );

        await webhookRepository.AddAsync(webhook, cancellationToken);
        events.CollectEvent(new WebhookCreated(webhook.Id));
        return WebhookResponse.From(webhook);
    }
}

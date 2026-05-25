using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Webhooks.Domain;
using SharedKernel.Domain;

namespace Main.Features.Webhooks.Shared;

[PublicAPI]
public sealed record WebhookResponse(
    WebhookId Id,
    UserId? UserId,
    EventTypeId? EventTypeId,
    string TargetUrl,
    string Secret,
    WebhookEventType[] EventSubscriptions,
    bool Active,
    DateTimeOffset CreatedAt
)
{
    public static WebhookResponse From(Webhook webhook)
    {
        return new WebhookResponse(
            webhook.Id,
            webhook.UserId,
            webhook.EventTypeId,
            webhook.TargetUrl,
            webhook.Secret,
            webhook.EventSubscriptions.ToArray(),
            webhook.Active,
            webhook.CreatedAt
        );
    }
}

[PublicAPI]
public sealed record WebhooksResponse(WebhookResponse[] Webhooks);

[PublicAPI]
public sealed record TestWebhookResponse(WebhookDeliveryId DeliveryId);

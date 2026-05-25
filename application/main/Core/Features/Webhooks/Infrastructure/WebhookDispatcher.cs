using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.Webhooks.Domain;
using SharedKernel.Domain;

namespace Main.Features.Webhooks.Infrastructure;

/// <summary>
///     Internal API used by feature handlers (booking, payment, recording…) to publish a domain
///     event onto the webhook delivery pipeline. Wiring booking lifecycle events into this
///     dispatcher is owned by track T3-booking-webhooks; this track only publishes the contract.
/// </summary>
[PublicAPI]
public interface IWebhookDispatcher
{
    /// <summary>
    ///     Enqueue a single delivery for a specific subscription. Used by the test-fire endpoint
    ///     and any caller that already knows the target webhook id.
    /// </summary>
    Task<WebhookDeliveryId> EnqueueAsync(WebhookId webhookId, WebhookEventType eventType, string payloadJson, CancellationToken cancellationToken);

    /// <summary>
    ///     Look up every active subscriber to <paramref name="eventType" /> for
    ///     <paramref name="tenantId" /> and enqueue one delivery per subscriber.
    /// </summary>
    Task<WebhookDeliveryId[]> FanOutAsync(TenantId tenantId, WebhookEventType eventType, string payloadJson, CancellationToken cancellationToken);
}

public sealed class WebhookDispatcher(
    IWebhookRepository webhookRepository,
    IWebhookDeliveryRepository deliveryRepository,
    TimeProvider timeProvider
) : IWebhookDispatcher
{
    public async Task<WebhookDeliveryId> EnqueueAsync(WebhookId webhookId, WebhookEventType eventType, string payloadJson, CancellationToken cancellationToken)
    {
        var webhook = await webhookRepository.GetByIdAsync(webhookId, cancellationToken)
                      ?? throw new InvalidOperationException($"Webhook {webhookId} not found.");

        var delivery = BuildDelivery(webhook, eventType, payloadJson);
        await deliveryRepository.AddAsync(delivery, cancellationToken);
        return delivery.Id;
    }

    public async Task<WebhookDeliveryId[]> FanOutAsync(TenantId tenantId, WebhookEventType eventType, string payloadJson, CancellationToken cancellationToken)
    {
        var subscribers = await webhookRepository.GetActiveSubscribersAsync(tenantId, eventType, cancellationToken);
        if (subscribers.Length == 0) return [];

        var ids = new WebhookDeliveryId[subscribers.Length];
        for (var i = 0; i < subscribers.Length; i++)
        {
            var delivery = BuildDelivery(subscribers[i], eventType, payloadJson);
            await deliveryRepository.AddAsync(delivery, cancellationToken);
            ids[i] = delivery.Id;
        }

        return ids;
    }

    private WebhookDelivery BuildDelivery(Webhook webhook, WebhookEventType eventType, string payloadJson)
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            [WebhookSigner.HeaderName] = WebhookSigner.Sign(webhook.Secret, payloadJson),
            ["User-Agent"] = "Nerova-Webhook/1.0"
        };
        var headersJson = JsonSerializer.Serialize(headers);

        return WebhookDelivery.Create(
            webhook.TenantId,
            webhook.Id,
            eventType,
            payloadJson,
            webhook.TargetUrl,
            headersJson,
            timeProvider.GetUtcNow()
        );
    }
}

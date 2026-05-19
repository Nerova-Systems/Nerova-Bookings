using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.BookingSideEffects.Domain;

[IdPrefix("whsub")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WebhookSubscriptionId>))]
public sealed record WebhookSubscriptionId(string Value) : StronglyTypedUlid<WebhookSubscriptionId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class WebhookSubscription : AggregateRoot<WebhookSubscriptionId>, ITenantScopedEntity
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    [UsedImplicitly]
    private WebhookSubscription() : base(WebhookSubscriptionId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        EventTypeId = new EventTypeId(string.Empty);
        SubscriberUrl = string.Empty;
        TriggersJson = "[]";
        PayloadFormat = string.Empty;
        PayloadVersion = string.Empty;
    }

    private WebhookSubscription(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        bool active,
        string subscriberUrl,
        string? secret,
        string[] triggers,
        string payloadFormat,
        string payloadVersion
    ) : base(WebhookSubscriptionId.NewId())
    {
        SubscriberUrl = string.Empty;
        TriggersJson = "[]";
        PayloadFormat = string.Empty;
        PayloadVersion = string.Empty;
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        EventTypeId = eventTypeId;
        Update(active, subscriberUrl, secret, triggers, payloadFormat, payloadVersion);
    }

    public UserId OwnerUserId { get; private set; }

    public EventTypeId EventTypeId { get; private set; }

    public bool Active { get; private set; }

    public string SubscriberUrl { get; private set; }

    public string? Secret { get; private set; }

    public string TriggersJson { get; private set; }

    public string PayloadFormat { get; private set; }

    public string PayloadVersion { get; private set; }

    [NotMapped]
    public string[] Triggers => JsonSerializer.Deserialize<string[]>(TriggersJson, JsonSerializerOptions) ?? [];

    public TenantId TenantId { get; } = new(0);

    public static WebhookSubscription Create(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        bool active,
        string subscriberUrl,
        string? secret,
        string[] triggers,
        string payloadFormat,
        string payloadVersion
    )
    {
        return new WebhookSubscription(tenantId, ownerUserId, eventTypeId, active, subscriberUrl, secret, triggers, payloadFormat, payloadVersion);
    }

    public void Update(bool active, string subscriberUrl, string? secret, string[] triggers, string payloadFormat, string payloadVersion)
    {
        Active = active;
        SubscriberUrl = subscriberUrl.Trim();
        Secret = string.IsNullOrWhiteSpace(secret) ? null : secret.Trim();
        TriggersJson = JsonSerializer.Serialize(triggers.Select(trigger => trigger.Trim().ToUpperInvariant()).Distinct().ToArray(), JsonSerializerOptions);
        PayloadFormat = string.IsNullOrWhiteSpace(payloadFormat) ? "cal-com" : payloadFormat.Trim();
        PayloadVersion = string.IsNullOrWhiteSpace(payloadVersion) ? "v1" : payloadVersion.Trim();
    }

    public void Disable()
    {
        Active = false;
    }
}

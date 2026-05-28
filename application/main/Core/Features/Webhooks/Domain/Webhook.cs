using System.Security.Cryptography;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Webhooks.Domain;

[IdPrefix("wh")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WebhookId>))]
public sealed record WebhookId(string Value) : StronglyTypedUlid<WebhookId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Webhook subscription owned by a tenant. Optionally scoped to a single user
///     (<see cref="UserId" />) or a single event type (<see cref="EventTypeId" />) — both null
///     means tenant-wide. <see cref="EventSubscriptions" /> enumerates the events this endpoint
///     wants delivered. <see cref="Secret" /> is the shared HMAC key used to sign request bodies.
/// </summary>
public sealed class Webhook : AggregateRoot<WebhookId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Webhook() : base(WebhookId.NewId())
    {
        TargetUrl = string.Empty;
        Secret = string.Empty;
        EventSubscriptionsJson = "[]";
    }

    private Webhook(
        TenantId tenantId,
        UserId? userId,
        EventTypeId? eventTypeId,
        string targetUrl,
        string secret,
        IReadOnlyCollection<WebhookEventType> eventSubscriptions,
        bool active
    ) : base(WebhookId.NewId())
    {
        TenantId = tenantId;
        UserId = userId;
        EventTypeId = eventTypeId;
        TargetUrl = targetUrl.Trim();
        Secret = secret;
        EventSubscriptionsJson = SerializeSubscriptions(eventSubscriptions);
        Active = active;
    }

    public UserId? UserId { get; private set; }

    public EventTypeId? EventTypeId { get; private set; }

    public string TargetUrl { get; private set; }

    /// <summary>Shared secret used as the HMAC-SHA256 key when signing delivery payloads.</summary>
    public string Secret { get; private set; }

    /// <summary>JSON-encoded array of <see cref="WebhookEventType" /> names. Use <see cref="EventSubscriptions" />.</summary>
    public string EventSubscriptionsJson { get; private set; }

    public bool Active { get; private set; } = true;

    public IReadOnlyCollection<WebhookEventType> EventSubscriptions => DeserializeSubscriptions(EventSubscriptionsJson);

    public TenantId TenantId { get; } = new(0);

    public static Webhook Create(
        TenantId tenantId,
        UserId? userId,
        EventTypeId? eventTypeId,
        string targetUrl,
        IReadOnlyCollection<WebhookEventType> eventSubscriptions,
        bool active = true,
        string? secret = null
    )
    {
        if (string.IsNullOrWhiteSpace(targetUrl)) throw new ArgumentException("Target URL is required.", nameof(targetUrl));
        if (eventSubscriptions is null || eventSubscriptions.Count == 0)
        {
            throw new ArgumentException("At least one event subscription is required.", nameof(eventSubscriptions));
        }

        return new Webhook(
            tenantId,
            userId,
            eventTypeId,
            targetUrl,
            secret ?? GenerateSecret(),
            eventSubscriptions,
            active
        );
    }

    public void Update(string targetUrl, IReadOnlyCollection<WebhookEventType> eventSubscriptions, bool active)
    {
        if (string.IsNullOrWhiteSpace(targetUrl)) throw new ArgumentException("Target URL is required.", nameof(targetUrl));
        if (eventSubscriptions is null || eventSubscriptions.Count == 0)
        {
            throw new ArgumentException("At least one event subscription is required.", nameof(eventSubscriptions));
        }

        TargetUrl = targetUrl.Trim();
        EventSubscriptionsJson = SerializeSubscriptions(eventSubscriptions);
        Active = active;
    }

    /// <summary>
    ///     Rotates the shared HMAC secret to a freshly generated value. Used by PUT
    ///     /api/webhooks/{id}?regenerateSecret=true. The cleartext is returned once via the
    ///     response and then masked on every subsequent read.
    /// </summary>
    public void RegenerateSecret()
    {
        Secret = GenerateSecret();
    }

    public bool IsSubscribedTo(WebhookEventType eventType)
    {
        return EventSubscriptions.Contains(eventType);
    }

    /// <summary>Generates a 32-byte random secret encoded as lowercase hex (64 characters).</summary>
    public static string GenerateSecret()
    {
        var buffer = new byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexStringLower(buffer);
    }

    private static string SerializeSubscriptions(IReadOnlyCollection<WebhookEventType> events)
    {
        return JsonSerializer.Serialize(events.Distinct().Select(e => e.ToString()).ToArray());
    }

    private static IReadOnlyCollection<WebhookEventType> DeserializeSubscriptions(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<WebhookEventType>();
        var names = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        var result = new List<WebhookEventType>(names.Length);
        foreach (var name in names)
        {
            if (Enum.TryParse<WebhookEventType>(name, out var parsed)) result.Add(parsed);
        }

        return result;
    }
}

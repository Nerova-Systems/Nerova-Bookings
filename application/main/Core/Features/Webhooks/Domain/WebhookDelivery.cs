using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Webhooks.Domain;

[IdPrefix("whd")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WebhookDeliveryId>))]
public sealed record WebhookDeliveryId(string Value) : StronglyTypedUlid<WebhookDeliveryId>(Value)
{
    public override string ToString() => Value;
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WebhookDeliveryStatus
{
    Pending,
    Succeeded,
    Failed,
    DeadLettered
}

/// <summary>
///     One attempt-tracking record per (webhook × event). The TickerQ delivery job polls for
///     <see cref="WebhookDeliveryStatus.Pending" /> rows whose <see cref="NextAttemptAt" /> has
///     passed, POSTs the payload, and updates this row. After
///     <see cref="MaxAttempts" /> failed attempts the row is moved to
///     <see cref="WebhookDeliveryStatus.DeadLettered" /> and is no longer retried.
/// </summary>
public sealed class WebhookDelivery : AggregateRoot<WebhookDeliveryId>, ITenantScopedEntity
{
    /// <summary>Maximum delivery attempts before dead-lettering. Mirrors cal.com's six-step backoff.</summary>
    public const int MaxAttempts = 6;

    /// <summary>
    ///     Maximum length of stored response bodies. Beyond this we truncate so a misbehaving target
    ///     cannot bloat the database with multi-megabyte HTML error pages.
    /// </summary>
    public const int MaxResponseBodyLength = 2_000;

    [UsedImplicitly]
    private WebhookDelivery() : base(WebhookDeliveryId.NewId())
    {
        WebhookId = new WebhookId(string.Empty);
        PayloadJson = string.Empty;
        RequestUrl = string.Empty;
        RequestHeadersJson = "{}";
    }

    private WebhookDelivery(
        TenantId tenantId,
        WebhookId webhookId,
        WebhookEventType eventType,
        string payloadJson,
        string requestUrl,
        string requestHeadersJson,
        DateTimeOffset firstAttemptAt
    ) : base(WebhookDeliveryId.NewId())
    {
        TenantId = tenantId;
        WebhookId = webhookId;
        EventType = eventType;
        PayloadJson = payloadJson;
        RequestUrl = requestUrl;
        RequestHeadersJson = requestHeadersJson;
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 0;
        NextAttemptAt = firstAttemptAt;
    }

    public TenantId TenantId { get; } = new(0);

    public WebhookId WebhookId { get; private set; }

    public WebhookEventType EventType { get; private set; }

    /// <summary>JSON-encoded payload body that was, or will be, sent to the target URL.</summary>
    public string PayloadJson { get; private set; }

    public string RequestUrl { get; private set; }

    /// <summary>JSON-encoded request headers snapshot (signature, content-type, custom headers).</summary>
    public string RequestHeadersJson { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>UTC timestamp at which the delivery job will next try to POST. Null when terminal.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public WebhookDeliveryStatus Status { get; private set; }

    public int? ResponseStatusCode { get; private set; }

    public string? ResponseBody { get; private set; }

    public static WebhookDelivery Create(
        TenantId tenantId,
        WebhookId webhookId,
        WebhookEventType eventType,
        string payloadJson,
        string requestUrl,
        string requestHeadersJson,
        DateTimeOffset firstAttemptAt
    )
    {
        return new WebhookDelivery(tenantId, webhookId, eventType, payloadJson, requestUrl, requestHeadersJson, firstAttemptAt);
    }

    public void RecordSuccess(int statusCode, string? responseBody, DateTimeOffset attemptAt)
    {
        AttemptCount++;
        LastAttemptAt = attemptAt;
        NextAttemptAt = null;
        Status = WebhookDeliveryStatus.Succeeded;
        ResponseStatusCode = statusCode;
        ResponseBody = Truncate(responseBody);
    }

    public void RecordFailure(int? statusCode, string? responseBody, DateTimeOffset attemptAt, DateTimeOffset? nextAttemptAt)
    {
        AttemptCount++;
        LastAttemptAt = attemptAt;
        ResponseStatusCode = statusCode;
        ResponseBody = Truncate(responseBody);

        if (nextAttemptAt is null || AttemptCount >= MaxAttempts)
        {
            Status = WebhookDeliveryStatus.DeadLettered;
            NextAttemptAt = null;
        }
        else
        {
            Status = WebhookDeliveryStatus.Failed;
            NextAttemptAt = nextAttemptAt;
        }
    }

    private static string? Truncate(string? value)
    {
        if (value is null) return null;
        return value.Length <= MaxResponseBodyLength ? value : value[..MaxResponseBodyLength];
    }
}

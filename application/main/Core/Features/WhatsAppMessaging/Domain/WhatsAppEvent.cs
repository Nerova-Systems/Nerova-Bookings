using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.WhatsAppMessaging.Domain;

[PublicAPI]
[IdPrefix("waevt")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, WhatsAppEventId>))]
public sealed record WhatsAppEventId(string Value) : StronglyTypedUlid<WhatsAppEventId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Durable inbox for Meta WhatsApp webhook deliveries. Two roles:
///     (1) dedup: <see cref="MetaEventId" /> (SHA-256 of raw body) prevents double-processing of
///     redelivered webhooks; (2) audit trail of all received payloads.
///     This table is effectively INSERT-only. Only the state machine columns
///     (<see cref="Status" />, <see cref="ProcessedAt" />, <see cref="Error" />) are updated as
///     the row progresses Pending → Processed (or Failed).
/// </summary>
public sealed class WhatsAppEvent : AggregateRoot<WhatsAppEventId>
{
    private WhatsAppEvent() : base(WhatsAppEventId.NewId())
    {
        Status = WhatsAppEventStatus.Pending;
        MetaEventId = string.Empty;
        Payload = string.Empty;
    }

    /// <summary>
    ///     SHA-256 hash of the raw webhook body. Used to deduplicate redeliveries. Has a unique index.
    /// </summary>
    public string MetaEventId { get; private set; }

    public WhatsAppEventStatus Status { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? Error { get; private set; }

    /// <summary>
    ///     Factory method for phase 1 webhook acknowledgment. Creates a Pending event to be
    ///     processed in phase 2.
    /// </summary>
    public static WhatsAppEvent Create(string metaEventId, string payload)
    {
        return new WhatsAppEvent { MetaEventId = metaEventId, Payload = payload };
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        EnsurePending();
        Status = WhatsAppEventStatus.Processed;
        ProcessedAt = processedAt;
    }

    public void MarkFailed(DateTimeOffset failedAt, string error)
    {
        EnsurePending();
        Status = WhatsAppEventStatus.Failed;
        ProcessedAt = failedAt;
        Error = error;
    }

    private void EnsurePending()
    {
        if (Status is not WhatsAppEventStatus.Pending)
        {
            throw new InvalidOperationException($"WhatsAppEvent '{Id.Value}' is no longer Pending (status: {Status}); refusing to mutate.");
        }
    }
}

public enum WhatsAppEventStatus
{
    Pending,
    Processed,
    Failed
}

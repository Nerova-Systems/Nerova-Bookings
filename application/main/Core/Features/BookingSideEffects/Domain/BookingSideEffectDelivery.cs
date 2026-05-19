using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.BookingSideEffects.Domain;

[IdPrefix("bfx")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingSideEffectDeliveryId>))]
public sealed record BookingSideEffectDeliveryId(string Value) : StronglyTypedUlid<BookingSideEffectDeliveryId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class BookingSideEffectDelivery : AggregateRoot<BookingSideEffectDeliveryId>, ITenantScopedEntity
{
    private const int MaximumAttempts = 5;

    [UsedImplicitly]
    private BookingSideEffectDelivery() : base(BookingSideEffectDeliveryId.NewId())
    {
        BookingId = new BookingId(string.Empty);
        EventTypeId = new EventTypeId(string.Empty);
        Trigger = string.Empty;
        Kind = string.Empty;
        Status = string.Empty;
        PayloadJson = "{}";
        DedupeKey = string.Empty;
    }

    private BookingSideEffectDelivery(
        TenantId tenantId,
        BookingId bookingId,
        EventTypeId eventTypeId,
        string trigger,
        string kind,
        string payloadJson,
        string dedupeKey,
        DateTimeOffset now
    ) : base(BookingSideEffectDeliveryId.NewId())
    {
        TenantId = tenantId;
        BookingId = bookingId;
        EventTypeId = eventTypeId;
        Trigger = trigger.Trim().ToUpperInvariant();
        Kind = kind.Trim().ToLowerInvariant();
        Status = BookingSideEffectConstants.PendingStatus;
        Attempts = 0;
        NextRetryAt = now;
        PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        DedupeKey = dedupeKey.Trim();
    }

    public BookingId BookingId { get; private set; }

    public EventTypeId EventTypeId { get; private set; }

    public string Trigger { get; private set; }

    public string Kind { get; private set; }

    public string Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? NextRetryAt { get; private set; }

    public string? LastError { get; private set; }

    public string PayloadJson { get; private set; }

    public string DedupeKey { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static BookingSideEffectDelivery Create(
        TenantId tenantId,
        BookingId bookingId,
        EventTypeId eventTypeId,
        string trigger,
        string kind,
        string payloadJson,
        string dedupeKey,
        DateTimeOffset now
    )
    {
        return new BookingSideEffectDelivery(tenantId, bookingId, eventTypeId, trigger, kind, payloadJson, dedupeKey, now);
    }

    public void MarkSent()
    {
        Status = BookingSideEffectConstants.SentStatus;
        LastError = null;
        NextRetryAt = null;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Delivery failed." : error.Trim();
        Status = Attempts >= MaximumAttempts ? BookingSideEffectConstants.FailedStatus : BookingSideEffectConstants.PendingStatus;
        NextRetryAt = Status == BookingSideEffectConstants.PendingStatus ? now.AddMinutes(Math.Min(Attempts * Attempts, 30)) : null;
    }
}

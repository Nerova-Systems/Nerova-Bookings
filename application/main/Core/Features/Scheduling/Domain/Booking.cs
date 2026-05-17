using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("book")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingId>))]
public sealed record BookingId(string Value) : StronglyTypedUlid<BookingId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class Booking : AggregateRoot<BookingId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Booking() : base(BookingId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        EventTypeId = new EventTypeId(string.Empty);
        BookerName = string.Empty;
        BookerEmail = string.Empty;
        TimeZone = string.Empty;
        Status = string.Empty;
        ResponsesJson = "{}";
    }

    private Booking(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        string bookerName,
        string bookerEmail,
        string timeZone,
        string status,
        Dictionary<string, string> responses
    ) : base(BookingId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        EventTypeId = eventTypeId;
        StartTime = startTime;
        EndTime = endTime;
        BeforeEventBufferMinutes = beforeEventBufferMinutes;
        AfterEventBufferMinutes = afterEventBufferMinutes;
        BookerName = bookerName.Trim();
        BookerEmail = bookerEmail.Trim().ToLowerInvariant();
        TimeZone = timeZone.Trim();
        Status = status.Trim();
        ResponsesJson = JsonSerializer.Serialize(responses);
    }

    public UserId OwnerUserId { get; private set; }

    public EventTypeId EventTypeId { get; private set; }

    public DateTimeOffset StartTime { get; private set; }

    public DateTimeOffset EndTime { get; private set; }

    public int BeforeEventBufferMinutes { get; private set; }

    public int AfterEventBufferMinutes { get; private set; }

    public string BookerName { get; private set; }

    public string BookerEmail { get; private set; }

    public string TimeZone { get; private set; }

    public string Status { get; private set; }

    public string ResponsesJson { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public void Cancel()
    {
        Status = "cancelled";
    }

    public static Booking Create(
        TenantId tenantId,
        UserId ownerUserId,
        EventTypeId eventTypeId,
        DateTimeOffset startTime,
        int durationMinutes,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        string bookerName,
        string bookerEmail,
        string timeZone,
        string status,
        Dictionary<string, string> responses
    )
    {
        return new Booking(tenantId, ownerUserId, eventTypeId, startTime, startTime.AddMinutes(durationMinutes), beforeEventBufferMinutes, afterEventBufferMinutes, bookerName, bookerEmail, timeZone, status, responses);
    }
}

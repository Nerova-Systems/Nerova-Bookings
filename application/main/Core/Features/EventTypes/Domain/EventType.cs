using Main.Features.Schedules.Domain;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.EventTypes.Domain;

[IdPrefix("etype")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, EventTypeId>))]
public sealed record EventTypeId(string Value) : StronglyTypedUlid<EventTypeId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class EventType : SoftDeletableAggregateRoot<EventTypeId>, ITenantScopedEntity
{
    private EventType() : base(EventTypeId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        Title = string.Empty;
        Slug = string.Empty;
        ScheduleId = new ScheduleId(string.Empty);
    }

    private EventType(
        TenantId tenantId,
        UserId ownerUserId,
        string title,
        string slug,
        string? description,
        int durationMinutes,
        bool hidden,
        ScheduleId scheduleId,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        int slotIntervalMinutes,
        int minimumBookingNoticeMinutes,
        string? locationType,
        string? locationValue
    ) : base(EventTypeId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Update(title, slug, description, durationMinutes, hidden, scheduleId, beforeEventBufferMinutes, afterEventBufferMinutes, slotIntervalMinutes, minimumBookingNoticeMinutes, locationType, locationValue);
    }

    public TenantId TenantId { get; private set; } = new(0);

    public UserId OwnerUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int DurationMinutes { get; private set; }

    public bool Hidden { get; private set; }

    public ScheduleId ScheduleId { get; private set; } = null!;

    public int BeforeEventBufferMinutes { get; private set; }

    public int AfterEventBufferMinutes { get; private set; }

    public int SlotIntervalMinutes { get; private set; }

    public int MinimumBookingNoticeMinutes { get; private set; }

    public string? LocationType { get; private set; }

    public string? LocationValue { get; private set; }

    public static EventType Create(
        TenantId tenantId,
        UserId ownerUserId,
        string title,
        string slug,
        string? description,
        int durationMinutes,
        bool hidden,
        ScheduleId scheduleId,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        int slotIntervalMinutes,
        int minimumBookingNoticeMinutes,
        string? locationType,
        string? locationValue
    )
    {
        return new EventType(tenantId, ownerUserId, title, slug, description, durationMinutes, hidden, scheduleId, beforeEventBufferMinutes, afterEventBufferMinutes, slotIntervalMinutes, minimumBookingNoticeMinutes, locationType, locationValue);
    }

    public void Update(
        string title,
        string slug,
        string? description,
        int durationMinutes,
        bool hidden,
        ScheduleId scheduleId,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        int slotIntervalMinutes,
        int minimumBookingNoticeMinutes,
        string? locationType,
        string? locationValue
    )
    {
        Title = title.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DurationMinutes = durationMinutes;
        Hidden = hidden;
        ScheduleId = scheduleId;
        BeforeEventBufferMinutes = beforeEventBufferMinutes;
        AfterEventBufferMinutes = afterEventBufferMinutes;
        SlotIntervalMinutes = slotIntervalMinutes;
        MinimumBookingNoticeMinutes = minimumBookingNoticeMinutes;
        LocationType = string.IsNullOrWhiteSpace(locationType) ? null : locationType.Trim();
        LocationValue = string.IsNullOrWhiteSpace(locationValue) ? null : locationValue.Trim();
    }
}

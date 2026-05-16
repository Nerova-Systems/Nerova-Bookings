using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;

namespace Main.Features.EventTypes.Shared;

[PublicAPI]
public sealed record EventTypeResponse(
    EventTypeId Id,
    string Title,
    string Slug,
    string? Description,
    int DurationMinutes,
    bool Hidden,
    ScheduleId ScheduleId,
    int BeforeEventBufferMinutes,
    int AfterEventBufferMinutes,
    int SlotIntervalMinutes,
    int MinimumBookingNoticeMinutes,
    string? LocationType,
    string? LocationValue
)
{
    public static EventTypeResponse From(EventType eventType)
    {
        return new EventTypeResponse(
            eventType.Id,
            eventType.Title,
            eventType.Slug,
            eventType.Description,
            eventType.DurationMinutes,
            eventType.Hidden,
            eventType.ScheduleId,
            eventType.BeforeEventBufferMinutes,
            eventType.AfterEventBufferMinutes,
            eventType.SlotIntervalMinutes,
            eventType.MinimumBookingNoticeMinutes,
            eventType.LocationType,
            eventType.LocationValue
        );
    }
}

[PublicAPI]
public sealed record EventTypesResponse(EventTypeResponse[] EventTypes);

using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using SharedKernel.Domain;

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
    string? LocationValue,
    EventTypeSettings Settings,
    bool IsInstantEvent,
    bool AssignAllTeamMembers,
    bool HideOrganizerEmail,
    bool BookingRequiresAuthentication,
    UserId? SecondaryEmailUserId
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
            eventType.LocationValue,
            eventType.Settings,
            eventType.IsInstantEvent,
            eventType.AssignAllTeamMembers,
            eventType.HideOrganizerEmail,
            eventType.BookingRequiresAuthentication,
            eventType.SecondaryEmailUserId
        );
    }
}

[PublicAPI]
public sealed record EventTypesResponse(EventTypeResponse[] EventTypes);

[PublicAPI]
public sealed record HashedLinkResponse(
    HashedLinkId Id,
    EventTypeId EventTypeId,
    string Hash,
    int? ExpiresAfterUses,
    DateTimeOffset? ExpiresAt
)
{
    public static HashedLinkResponse From(HashedLink link)
    {
        return new HashedLinkResponse(link.Id, link.EventTypeId, link.Hash, link.ExpiresAfterUses, link.ExpiresAt);
    }
}

[PublicAPI]
public sealed record HashedLinksResponse(HashedLinkResponse[] HashedLinks);

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

// ─── Queries deferred from initial deliverable ──────────────────────────────

[PublicAPI]
public sealed record EventTypeGroupResponse(string Kind, TenantId? TeamId, EventTypeResponse[] EventTypes);

/// <summary>
///     Event types visible to the caller, grouped by ownership scope.
///     "personal" = caller-owned, not team-scoped.
///     "team"     = team-scoped, caller owns or is a host.
///     Org-level event types are NOT included: main has no membership data to determine
///     organization membership; that bucket is deferred until cross-SCS membership is exposed.
/// </summary>
[PublicAPI]
public sealed record EventTypesByViewerResponse(EventTypeGroupResponse[] Groups);

[PublicAPI]
public sealed record EventTypeGroupSummaryResponse(string Kind, TenantId? TeamId, int Count);

[PublicAPI]
public sealed record EventTypeGroupsResponse(EventTypeGroupSummaryResponse[] Groups);

[PublicAPI]
public sealed record HostCandidateResponse(UserId UserId);

[PublicAPI]
public sealed record HostsForAssignmentResponse(HostCandidateResponse[] Candidates);

[PublicAPI]
public sealed record HostAvailabilityWindowResponse(int[] Days, int StartMinute, int EndMinute);

[PublicAPI]
public sealed record HostAvailabilityResponse(
    UserId UserId,
    string TimeZone,
    HostAvailabilityWindowResponse[] AvailabilityWindows
);

[PublicAPI]
public sealed record HostsForAvailabilityResponse(HostAvailabilityResponse[] Hosts);

[PublicAPI]
public sealed record BulkApplyLocationsItem(EventTypeId EventTypeId, string? LocationType, string? LocationValue);

[PublicAPI]
public sealed record BulkApplyLocationsResponse(EventTypeId[] Updated);

using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;

namespace Main.Features.Scheduling.Shared;

[PublicAPI]
public sealed record PublicSchedulingProfileResponse(string DisplayName, string? AvatarUrl)
{
    public static PublicSchedulingProfileResponse From(SchedulingProfile profile)
    {
        return new PublicSchedulingProfileResponse(profile.DisplayName, profile.AvatarUrl);
    }
}

[PublicAPI]
public sealed record PublicEventTypeResponse(
    string Handle,
    string Slug,
    string Title,
    string? Description,
    int DurationMinutes,
    int[] DurationOptions,
    int BeforeEventBufferMinutes,
    int AfterEventBufferMinutes,
    int SlotIntervalMinutes,
    int MinimumBookingNoticeMinutes,
    string? LocationType,
    string? LocationValue,
    EventTypeLocation[] Locations,
    EventTypeBookingField[] BookingFields,
    EventTypeBookingWindow BookingWindow,
    EventTypeConfirmationPolicy ConfirmationPolicy,
    EventTypeRecurrence? Recurrence,
    EventTypeSeats Seats,
    string? WabaPhoneNumber,
    PublicSchedulingProfileResponse Profile
)
{
    public static PublicEventTypeResponse From(SchedulingProfile profile, EventType eventType, string? wabaPhoneNumber = null)
    {
        return new PublicEventTypeResponse(
            profile.Handle,
            eventType.Slug,
            eventType.Title,
            eventType.Description,
            eventType.DurationMinutes,
            eventType.DurationOptions,
            eventType.BeforeEventBufferMinutes,
            eventType.AfterEventBufferMinutes,
            eventType.SlotIntervalMinutes,
            eventType.MinimumBookingNoticeMinutes,
            eventType.LocationType,
            eventType.LocationValue,
            eventType.Settings.Locations,
            eventType.Settings.BookingFields,
            eventType.Settings.BookingWindow,
            eventType.Settings.ConfirmationPolicy,
            eventType.Settings.Recurrence,
            eventType.Settings.Seats,
            wabaPhoneNumber,
            PublicSchedulingProfileResponse.From(profile)
        );
    }
}

[PublicAPI]
public sealed record PublicSlotsResponse(Dictionary<string, PublicSlotResponse[]> Slots);

[PublicAPI]
public sealed record PublicSlotResponse(DateTimeOffset Time, DateTimeOffset EndTime, int? Attendees = null, BookingId? BookingUid = null);

[PublicAPI]
public sealed record CreatePublicBookingResponse(BookingId Id, DateTimeOffset StartTime, DateTimeOffset EndTime, string Status);

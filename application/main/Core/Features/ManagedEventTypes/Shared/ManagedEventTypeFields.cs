namespace Main.Features.ManagedEventTypes.Shared;

/// <summary>
///     String constants for every field name that can appear in <c>UnlockedFields</c> on a managed event type.
///     These values are stored in the database and used for lock/unlock checks.
/// </summary>
public static class ManagedEventTypeFields
{
    public const string Title = "title";
    public const string Slug = "slug";
    public const string Description = "description";
    public const string DurationMinutes = "durationMinutes";
    public const string Hidden = "hidden";
    public const string ScheduleId = "scheduleId";
    public const string BeforeEventBufferMinutes = "beforeEventBufferMinutes";
    public const string AfterEventBufferMinutes = "afterEventBufferMinutes";
    public const string SlotIntervalMinutes = "slotIntervalMinutes";
    public const string MinimumBookingNoticeMinutes = "minimumBookingNoticeMinutes";
    public const string LocationType = "locationType";
    public const string LocationValue = "locationValue";
    public const string Settings = "settings";

    public static readonly string[] All =
    [
        Title, Slug, Description, DurationMinutes, Hidden, ScheduleId,
        BeforeEventBufferMinutes, AfterEventBufferMinutes, SlotIntervalMinutes,
        MinimumBookingNoticeMinutes, LocationType, LocationValue, Settings
    ];
}

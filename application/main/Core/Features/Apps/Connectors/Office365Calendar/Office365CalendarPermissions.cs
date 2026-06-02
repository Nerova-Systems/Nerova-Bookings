using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>
///     The real Microsoft Office 365 (Outlook) Calendar OAuth scopes surfaced through the Apps
///     API. Scope strings are referenced from <see cref="Office365CalendarOptions" /> so this
///     descriptor list and the scopes actually requested at authorize time share a single source
///     of truth.
/// </summary>
public static class Office365CalendarPermissions
{
    public static readonly IReadOnlyList<AppPermission> All =
    [
        new AppPermission(
            Office365CalendarOptions.OfflineAccessScope,
            "Stay connected",
            "Refreshes access in the background so you do not have to sign in again for every booking."
        ),
        new AppPermission(
            Office365CalendarOptions.CalendarsReadWriteScope,
            "Read and manage calendars",
            "Reads your calendars to check availability and creates, updates, or cancels events for bookings."
        ),
        new AppPermission(
            Office365CalendarOptions.OnlineMeetingsScope,
            "Manage Teams meetings",
            "Creates and manages Microsoft Teams online meeting links for your bookings."
        )
    ];
}

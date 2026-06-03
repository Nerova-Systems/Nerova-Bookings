using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     The real Google Calendar OAuth scopes surfaced through the Apps API. Scope strings are
///     referenced from <see cref="GoogleCalendarOptions" /> so this descriptor list and the
///     scopes actually requested at authorize time share a single source of truth.
/// </summary>
public static class GoogleCalendarPermissions
{
    public static readonly IReadOnlyList<AppPermission> All =
    [
        new(
            GoogleCalendarOptions.CalendarReadonlyScope,
            "View your calendars",
            "Reads your calendars and existing events to check availability and prevent double-booking."
        ),
        new(
            GoogleCalendarOptions.CalendarEventsScope,
            "Manage calendar events",
            "Creates, updates, and cancels calendar events on your behalf for confirmed bookings."
        )
    ];
}

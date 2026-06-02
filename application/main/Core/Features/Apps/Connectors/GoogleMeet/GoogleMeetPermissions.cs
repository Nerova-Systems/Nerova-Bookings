using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Domain;

namespace Main.Features.Apps.Connectors.GoogleMeet;

/// <summary>
///     Google Meet runs no OAuth flow of its own — it generates meeting links by creating
///     Google Calendar events with conferencing data, reusing the host's Google Calendar
///     credential. Its permissions are therefore the real Google Calendar scopes (referenced
///     from <see cref="GoogleCalendarOptions" /> for a single source of truth), described in
///     terms of the Google Calendar connection it operates through.
/// </summary>
public static class GoogleMeetPermissions
{
    public static readonly IReadOnlyList<AppPermission> All =
    [
        new AppPermission(
            GoogleCalendarOptions.CalendarReadonlyScope,
            "View your calendars",
            "Uses your Google Calendar connection to read calendars and check availability."
        ),
        new AppPermission(
            GoogleCalendarOptions.CalendarEventsScope,
            "Manage calendar events",
            "Uses your Google Calendar connection to create events with Google Meet conferencing links."
        )
    ];
}

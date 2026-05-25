namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>
///     Connector-neutral booking payload passed to <see cref="Office365CalendarService.CreateEventAsync" />
///     and <see cref="Office365CalendarService.UpdateEventAsync" />. Carries only the fields
///     Microsoft Graph needs (start/end, title, description, organizer/attendees, time zone) —
///     intentionally decoupled from the <c>Booking</c> aggregate so the connector layer never
///     pulls scheduling internals into its surface. Shape mirrors the Google connector's
///     <c>BookingEvent</c> so a future shared abstraction can lift one or the other unchanged.
/// </summary>
public sealed record BookingEvent(
    string Title,
    string? Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string TimeZone,
    string OrganizerEmail,
    string? OrganizerName,
    IReadOnlyList<BookingEventAttendee> Attendees,
    string? Location = null,
    string? ICalUid = null
);

public sealed record BookingEventAttendee(string Email, string? Name);

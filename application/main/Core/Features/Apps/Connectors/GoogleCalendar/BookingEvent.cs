namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     Connector-neutral booking payload passed to <see cref="GoogleCalendarService.CreateEventAsync" />
///     and <see cref="GoogleCalendarService.UpdateEventAsync" />. Carries only the fields the
///     external calendar needs (start/end, title, description, organizer/attendees, time zone) —
///     intentionally decoupled from the <c>Booking</c> aggregate so the connector layer never
///     pulls scheduling internals into its surface.
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

namespace Main.Features.Apps.Domain;

/// <summary>
///     Connector-neutral booking payload passed to <see cref="IConferenceLinkProvider" />
///     implementations. Carries only the fields a video meeting provider needs (title,
///     start/end, organizer, attendees, time zone). Intentionally decoupled from the
///     <c>Booking</c> aggregate so connectors never pull scheduling internals into their
///     surface.
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
    string? ICalUid = null
);

public sealed record BookingEventAttendee(string Email, string? Name);

/// <summary>
///     The provider's response after creating or updating a conferencing meeting. Carries the
///     three fields the scheduling layer needs to surface to bookers: the external id (so the
///     local <c>BookingReference</c> can later update / cancel the same meeting), the join URL
///     (persisted on <c>Booking.LocationValue</c>), and the optional password.
/// </summary>
public sealed record ConferenceLink(string ExternalId, string JoinUrl, string? Password);

/// <summary>
///     Contract every conferencing connector (Zoom, Google Meet, MS Teams, …) implements so
///     the scheduling layer can create / update / cancel the external meeting without taking a
///     hard dependency on any specific provider SDK. The provider receives the already-loaded
///     <see cref="Credential" /> for the host: decrypting and refreshing the token is the
///     implementation's concern.
///     <para>
///         Implementations are registered as scoped services so the per-credential service
///         factory (which depends on the request-scoped <c>ICredentialRepository</c>) can flow
///         the rotated token back into the database.
///     </para>
/// </summary>
public interface IConferenceLinkProvider
{
    /// <summary>The <see cref="AppSlug" /> this provider handles. Used by callers to look up
    ///     the right provider from the registered set.</summary>
    AppSlug Slug { get; }

    Task<ConferenceLink> CreateAsync(Credential credential, BookingEvent input, CancellationToken cancellationToken);

    Task<ConferenceLink> UpdateAsync(Credential credential, string externalId, BookingEvent input, CancellationToken cancellationToken);

    Task CancelAsync(Credential credential, string externalId, CancellationToken cancellationToken);
}

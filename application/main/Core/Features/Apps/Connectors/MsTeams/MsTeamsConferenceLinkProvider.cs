using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Domain;
using DomainBookingEvent = Main.Features.Apps.Domain.BookingEvent;
using O365BookingEvent = Main.Features.Apps.Connectors.Office365Calendar.BookingEvent;
using O365Attendee = Main.Features.Apps.Connectors.Office365Calendar.BookingEventAttendee;

namespace Main.Features.Apps.Connectors.MsTeams;

/// <summary>
///     Adapts the Microsoft Graph <c>/me/onlineMeetings</c> endpoint to the generic
///     <see cref="IConferenceLinkProvider" /> contract. MS Teams reuses the Office 365
///     Calendar credential (<see cref="CredentialAppSlug" /> returns <c>office365-calendar</c>),
///     so the orchestrator looks up the existing Calendar credential rather than a
///     (non-existent) ms-teams credential. Token refresh + persistence flow through
///     <see cref="Office365CalendarServiceFactory" />, the same code path Calendar uses, so
///     a single refresh event updates both connectors simultaneously.
/// </summary>
public sealed class MsTeamsConferenceLinkProvider(Office365CalendarServiceFactory factory) : IConferenceLinkProvider
{
    public AppSlug Slug => MsTeamsSlug.Slug;

    public AppSlug CredentialAppSlug => Office365CalendarSlug.Slug;

    public async Task<ConferenceLink> CreateAsync(Credential credential, DomainBookingEvent input, CancellationToken cancellationToken)
    {
        var service = factory.Create(credential);
        var (id, joinUrl) = await service.CreateOnlineMeetingAsync(ToO365(input), cancellationToken);
        // Teams meetings carry no separate password — the joinUrl itself is the access token.
        return new ConferenceLink(id, joinUrl, Password: null);
    }

    public async Task<ConferenceLink> UpdateAsync(Credential credential, string externalId, DomainBookingEvent input, CancellationToken cancellationToken)
    {
        var service = factory.Create(credential);
        var (id, joinUrl) = await service.UpdateOnlineMeetingAsync(externalId, ToO365(input), cancellationToken);
        return new ConferenceLink(id, joinUrl, Password: null);
    }

    public Task CancelAsync(Credential credential, string externalId, CancellationToken cancellationToken)
    {
        var service = factory.Create(credential);
        return service.CancelOnlineMeetingAsync(externalId, cancellationToken);
    }

    private static O365BookingEvent ToO365(DomainBookingEvent input)
    {
        // The onlineMeetings endpoint ignores attendees/description/timezone — we only carry
        // the fields BuildOnlineMeetingBody actually serializes (title + start/end).
        var attendees = input.Attendees
            .Select(a => new O365Attendee(a.Email, a.Name))
            .ToArray();
        return new O365BookingEvent(
            input.Title,
            input.Description,
            input.StartTime,
            input.EndTime,
            input.TimeZone,
            input.OrganizerEmail,
            input.OrganizerName,
            attendees,
            Location: null,
            ICalUid: input.ICalUid
        );
    }
}

using Main.Features.Connectors.Domain;
using Main.Features.EventTypes.Domain;

namespace Main.Features.Connectors.Shared;

public sealed record CoreConnectorAccountsResponse(CoreConnectorAccountResponse[] Accounts);

public sealed record CoreConnectorAccountResponse(
    string Id,
    string Integration,
    string ExternalAccountId,
    string AccountEmail,
    string DisplayName,
    string Status,
    CoreConnectorCalendarResponse[] Calendars
)
{
    public static CoreConnectorAccountResponse From(ConnectorCredential credential)
    {
        return new CoreConnectorAccountResponse(
            credential.Id,
            credential.Integration,
            credential.ExternalAccountId,
            credential.AccountEmail,
            credential.DisplayName,
            credential.Status,
            credential.Calendars.Select(calendar => new CoreConnectorCalendarResponse(calendar.ExternalId, calendar.Name, calendar.Primary)).ToArray()
        );
    }
}

public sealed record CoreConnectorCalendarResponse(string ExternalId, string Name, bool Primary);

public sealed record UpdateSelectedCalendarsRequest(EventTypeSelectedCalendar[] SelectedCalendars);

public sealed record UpdateDestinationCalendarRequest(EventTypeDestinationCalendar? DestinationCalendar);

public sealed record UpdateDefaultConferencingRequest(EventTypeDefaultConferencing? DefaultConferencing);

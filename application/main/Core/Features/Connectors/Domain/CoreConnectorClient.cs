using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Domain;

namespace Main.Features.Connectors.Domain;

public sealed record CalendarBusyWindow(DateTimeOffset StartTime, DateTimeOffset EndTime);

public interface ICoreConnectorClient
{
    Task<CalendarBusyWindow[]> GetBusyWindowsAsync(
        TenantId tenantId,
        EventTypeSelectedCalendar[] selectedCalendars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    );

    Task<BookingReference> CreateCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken);

    Task<BookingReference> UpdateCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken);

    Task DeleteCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken);

    Task<BookingReference> CreateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken);

    Task<BookingReference> UpdateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken);

    Task DeleteMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken);
}

public sealed class CoreConnectorClient(
    IConnectorCredentialRepository connectorCredentialRepository,
    IEnumerable<ICoreConnectorProvider> providers,
    FakeCoreConnectorClient fakeCoreConnectorClient
) : ICoreConnectorClient
{
    public async Task<CalendarBusyWindow[]> GetBusyWindowsAsync(
        TenantId tenantId,
        EventTypeSelectedCalendar[] selectedCalendars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    )
    {
        var credentialIds = selectedCalendars
            .Select(calendar => calendar.CredentialId)
            .Where(id => !string.IsNullOrWhiteSpace(id) && !IsFakeCredentialId(id))
            .Select(id => id!)
            .ToArray();
        var credentials = await connectorCredentialRepository.GetForTenantByIdsAsync(tenantId, credentialIds, cancellationToken);
        var busyWindows = new List<CalendarBusyWindow>();

        foreach (var provider in providers)
        {
            var providerSelectedCalendars = selectedCalendars
                .Where(calendar => provider.Supports(calendar.Integration))
                .Where(calendar => credentials.Any(credential => credential.Id == calendar.CredentialId))
                .ToArray();
            if (providerSelectedCalendars.Length == 0) continue;

            busyWindows.AddRange(await provider.GetBusyWindowsAsync(credentials, providerSelectedCalendars, startTime, endTime, cancellationToken));
        }

        busyWindows.AddRange(await fakeCoreConnectorClient.GetBusyWindowsAsync(tenantId, selectedCalendars, startTime, endTime, cancellationToken));
        return busyWindows
            .OrderBy(window => window.StartTime)
            .ThenBy(window => window.EndTime)
            .ToArray();
    }

    public Task<BookingReference> CreateCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken)
    {
        return fakeCoreConnectorClient.CreateCalendarEventAsync(booking, destinationCalendar, cancellationToken);
    }

    public Task<BookingReference> UpdateCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken)
    {
        return fakeCoreConnectorClient.UpdateCalendarEventAsync(booking, destinationCalendar, cancellationToken);
    }

    public Task DeleteCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken)
    {
        return fakeCoreConnectorClient.DeleteCalendarEventAsync(booking, destinationCalendar, cancellationToken);
    }

    public Task<BookingReference> CreateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        return fakeCoreConnectorClient.CreateMeetingAsync(booking, conferencing, cancellationToken);
    }

    public Task<BookingReference> UpdateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        return fakeCoreConnectorClient.UpdateMeetingAsync(booking, conferencing, cancellationToken);
    }

    public Task DeleteMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        return fakeCoreConnectorClient.DeleteMeetingAsync(booking, conferencing, cancellationToken);
    }

    private static bool IsFakeCredentialId(string? credentialId)
    {
        return credentialId?.StartsWith("fake-busy:", StringComparison.OrdinalIgnoreCase) == true ||
               credentialId?.StartsWith("e2e-office365-calendar:", StringComparison.OrdinalIgnoreCase) == true ||
               credentialId?.StartsWith("e2e-zoom-video:", StringComparison.OrdinalIgnoreCase) == true;
    }
}

public sealed class FakeCoreConnectorClient
{
    private const string FakeBusyPrefix = "fake-busy:";

    public Task<CalendarBusyWindow[]> GetBusyWindowsAsync(
        TenantId tenantId,
        EventTypeSelectedCalendar[] selectedCalendars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    )
    {
        var busyWindows = selectedCalendars
            .Where(calendar => CoreConnectorConstants.IsCoreCalendar(calendar.Integration))
            .SelectMany(calendar => ParseFakeBusyWindows(calendar.CredentialId))
            .Where(window => window.StartTime < endTime && window.EndTime > startTime)
            .ToArray();

        return Task.FromResult(busyWindows);
    }

    public Task<BookingReference> CreateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        CancellationToken cancellationToken
    )
    {
        var integration = destinationCalendar.Integration.Trim();
        var uid = $"{integration}-{booking.Id.Value}";
        return Task.FromResult(
            new BookingReference(
                integration,
                uid,
                null,
                null,
                null,
                destinationCalendar.ExternalId,
                false
            )
        );
    }

    public Task<BookingReference> UpdateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        CancellationToken cancellationToken
    )
    {
        return CreateCalendarEventAsync(booking, destinationCalendar, cancellationToken);
    }

    public Task DeleteCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<BookingReference> CreateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        var app = conferencing.App.Trim();
        var meetingId = $"{app}-{booking.Id.Value}";
        return Task.FromResult(
            new BookingReference(
                app,
                meetingId,
                meetingId,
                app.Equals(CoreConnectorConstants.ZoomVideo, StringComparison.OrdinalIgnoreCase) ? "zoom-passcode" : null,
                MeetingUrl(app, meetingId),
                null,
                false
            )
        );
    }

    public Task<BookingReference> UpdateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        return CreateMeetingAsync(booking, conferencing, cancellationToken);
    }

    public Task DeleteMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static IEnumerable<CalendarBusyWindow> ParseFakeBusyWindows(string? credentialId)
    {
        if (string.IsNullOrWhiteSpace(credentialId) || !credentialId.StartsWith(FakeBusyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var rawWindows = credentialId[FakeBusyPrefix.Length..];
        var ownerScopeSeparatorIndex = rawWindows.IndexOf('|');
        if (ownerScopeSeparatorIndex >= 0)
        {
            rawWindows = rawWindows[(ownerScopeSeparatorIndex + 1)..];
        }

        var windows = rawWindows.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var window in windows)
        {
            var parts = window.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            if (!DateTimeOffset.TryParse(parts[0], out var startTime) || !DateTimeOffset.TryParse(parts[1], out var endTime)) continue;
            if (endTime <= startTime) continue;

            yield return new CalendarBusyWindow(startTime, endTime);
        }
    }

    private static string MeetingUrl(string app, string meetingId)
    {
        return app.ToLowerInvariant() switch
        {
            CoreConnectorConstants.GoogleMeet => $"https://meet.google.com/{meetingId}",
            CoreConnectorConstants.Office365Video => $"https://teams.microsoft.com/l/meetup-join/{meetingId}",
            CoreConnectorConstants.ZoomVideo => $"https://zoom.example.test/j/{meetingId}",
            _ => $"https://meet.example.test/{meetingId}"
        };
    }
}

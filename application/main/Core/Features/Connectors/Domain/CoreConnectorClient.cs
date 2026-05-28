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

    Task<BookingCalReference> CreateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    );

    Task<BookingCalReference> UpdateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    );

    Task DeleteCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken);

    Task<BookingCalReference> CreateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken);

    Task<BookingCalReference> UpdateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken);

    Task DeleteMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken);
}

public sealed class CoreConnectorClient(
    IConnectorCredentialRepository connectorCredentialRepository,
    IEnumerable<ICoreConnectorProvider> providers,
    IEnumerable<ICoreCalendarConnectorProvider> calendarProviders,
    IEnumerable<ICoreConferencingConnectorProvider> conferencingProviders,
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

    public async Task<BookingCalReference> CreateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        var providerContext = await ResolveCalendarProviderAsync(booking, destinationCalendar, cancellationToken);
        return providerContext is null
            ? await fakeCoreConnectorClient.CreateCalendarEventAsync(booking, destinationCalendar, conferencing, cancellationToken)
            : await providerContext.Provider.CreateCalendarEventAsync(providerContext.Credential, booking, destinationCalendar, conferencing, cancellationToken);
    }

    public async Task<BookingCalReference> UpdateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        var existingReference = FindActiveReference(booking, destinationCalendar.Integration, destinationCalendar.ExternalId);
        if (existingReference is null)
        {
            return await CreateCalendarEventAsync(booking, destinationCalendar, conferencing, cancellationToken);
        }

        var providerContext = await ResolveCalendarProviderAsync(booking, destinationCalendar, cancellationToken);
        return providerContext is null
            ? await fakeCoreConnectorClient.UpdateCalendarEventAsync(booking, destinationCalendar, conferencing, cancellationToken)
            : await providerContext.Provider.UpdateCalendarEventAsync(providerContext.Credential, booking, destinationCalendar, existingReference, conferencing, cancellationToken);
    }

    public async Task DeleteCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken)
    {
        var existingReference = FindActiveReference(booking, destinationCalendar.Integration, destinationCalendar.ExternalId);
        if (existingReference is null) return;

        var providerContext = await ResolveCalendarProviderAsync(booking, destinationCalendar, cancellationToken);
        if (providerContext is null)
        {
            await fakeCoreConnectorClient.DeleteCalendarEventAsync(booking, destinationCalendar, cancellationToken);
            return;
        }

        await providerContext.Provider.DeleteCalendarEventAsync(providerContext.Credential, booking, destinationCalendar, existingReference, cancellationToken);
    }

    public async Task<BookingCalReference> CreateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        var providerContext = await ResolveConferencingProviderAsync(booking, conferencing, cancellationToken);
        return providerContext is null
            ? await fakeCoreConnectorClient.CreateMeetingAsync(booking, conferencing, cancellationToken)
            : await providerContext.Provider.CreateMeetingAsync(providerContext.Credential, booking, conferencing, cancellationToken);
    }

    public async Task<BookingCalReference> UpdateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        var existingReference = FindActiveReference(booking, conferencing.App, null);
        if (existingReference is null)
        {
            return await CreateMeetingAsync(booking, conferencing, cancellationToken);
        }

        var providerContext = await ResolveConferencingProviderAsync(booking, conferencing, cancellationToken);
        return providerContext is null
            ? await fakeCoreConnectorClient.UpdateMeetingAsync(booking, conferencing, cancellationToken)
            : await providerContext.Provider.UpdateMeetingAsync(providerContext.Credential, booking, existingReference, conferencing, cancellationToken);
    }

    public async Task DeleteMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        var existingReference = FindActiveReference(booking, conferencing.App, null);
        if (existingReference is null) return;

        var providerContext = await ResolveConferencingProviderAsync(booking, conferencing, cancellationToken);
        if (providerContext is null)
        {
            await fakeCoreConnectorClient.DeleteMeetingAsync(booking, conferencing, cancellationToken);
            return;
        }

        await providerContext.Provider.DeleteMeetingAsync(providerContext.Credential, booking, existingReference, conferencing, cancellationToken);
    }

    private async Task<CalendarProviderContext?> ResolveCalendarProviderAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(destinationCalendar.CredentialId) || IsFakeCredentialId(destinationCalendar.CredentialId)) return null;

        var credentials = await connectorCredentialRepository.GetForTenantByIdsAsync(booking.TenantId, [destinationCalendar.CredentialId], cancellationToken);
        var credential = credentials.SingleOrDefault();
        if (credential is null) throw new HttpRequestException($"Connector credential '{destinationCalendar.CredentialId}' was not found.");
        var provider = calendarProviders.FirstOrDefault(candidate => candidate.Supports(destinationCalendar.Integration));
        if (provider is null) throw new HttpRequestException($"Calendar connector provider '{destinationCalendar.Integration}' is not supported.");
        if (!provider.Supports(credential.Integration))
        {
            throw new HttpRequestException($"Connector credential '{credential.Id}' does not support calendar integration '{destinationCalendar.Integration}'.");
        }

        return new CalendarProviderContext(provider, credential);
    }

    private async Task<ConferencingProviderContext?> ResolveConferencingProviderAsync(
        Booking booking,
        EventTypeDefaultConferencing conferencing,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(conferencing.CredentialId) || IsFakeCredentialId(conferencing.CredentialId)) return null;

        var credentials = await connectorCredentialRepository.GetForTenantByIdsAsync(booking.TenantId, [conferencing.CredentialId], cancellationToken);
        var credential = credentials.SingleOrDefault();
        if (credential is null) throw new HttpRequestException($"Connector credential '{conferencing.CredentialId}' was not found.");
        var provider = conferencingProviders.FirstOrDefault(candidate => candidate.SupportsConferencing(conferencing.App));
        if (provider is null) throw new HttpRequestException($"Conferencing connector provider '{conferencing.App}' is not supported.");
        if (!credential.Integration.Equals(conferencing.App, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException($"Connector credential '{credential.Id}' does not support conferencing app '{conferencing.App}'.");
        }

        return new ConferencingProviderContext(provider, credential);
    }

    private static BookingCalReference? FindActiveReference(Booking booking, string type, string? externalCalendarId)
    {
        return booking.References.FirstOrDefault(reference =>
            !reference.Deleted &&
            reference.Type.Equals(type, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(externalCalendarId) ||
             string.IsNullOrWhiteSpace(reference.ExternalCalendarId) ||
             reference.ExternalCalendarId.Equals(externalCalendarId, StringComparison.OrdinalIgnoreCase))
        );
    }

    private static bool IsFakeCredentialId(string? credentialId)
    {
        return credentialId?.StartsWith("fake-", StringComparison.OrdinalIgnoreCase) == true ||
               credentialId?.StartsWith("fake-busy:", StringComparison.OrdinalIgnoreCase) == true ||
               credentialId?.StartsWith("e2e-office365-calendar:", StringComparison.OrdinalIgnoreCase) == true ||
               credentialId?.StartsWith("e2e-zoom-video:", StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed record CalendarProviderContext(ICoreCalendarConnectorProvider Provider, ConnectorCredential Credential);

    private sealed record ConferencingProviderContext(ICoreConferencingConnectorProvider Provider, ConnectorCredential Credential);
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

    public Task<BookingCalReference> CreateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        var integration = destinationCalendar.Integration.Trim();
        var uid = $"{integration}-{booking.Id.Value}";
        return Task.FromResult(
            new BookingCalReference(
                integration,
                uid,
                IsCalendarConferencing(integration, conferencing) ? uid : null,
                null,
                IsCalendarConferencing(integration, conferencing) ? MeetingUrl(conferencing!.App, uid) : null,
                destinationCalendar.ExternalId,
                false
            )
        );
    }

    public Task<BookingCalReference> UpdateCalendarEventAsync(
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        return CreateCalendarEventAsync(booking, destinationCalendar, conferencing, cancellationToken);
    }

    public Task DeleteCalendarEventAsync(Booking booking, EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<BookingCalReference> CreateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        var app = conferencing.App.Trim();
        var meetingId = $"{app}-{booking.Id.Value}";
        return Task.FromResult(
            new BookingCalReference(
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

    public Task<BookingCalReference> UpdateMeetingAsync(Booking booking, EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
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

    private static bool IsCalendarConferencing(string calendarIntegration, EventTypeDefaultConferencing? conferencing)
    {
        return conferencing is not null &&
               ((calendarIntegration.Equals(CoreConnectorConstants.GoogleCalendar, StringComparison.OrdinalIgnoreCase) &&
                 conferencing.App.Equals(CoreConnectorConstants.GoogleMeet, StringComparison.OrdinalIgnoreCase)) ||
                (calendarIntegration.Equals(CoreConnectorConstants.Office365Calendar, StringComparison.OrdinalIgnoreCase) &&
                 conferencing.App.Equals(CoreConnectorConstants.Office365Video, StringComparison.OrdinalIgnoreCase)));
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

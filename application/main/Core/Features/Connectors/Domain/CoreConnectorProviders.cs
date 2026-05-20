using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Microsoft.Extensions.Configuration;

namespace Main.Features.Connectors.Domain;

public interface ICoreConnectorProvider
{
    bool Supports(string integration);

    Task<CalendarBusyWindow[]> GetBusyWindowsAsync(
        ConnectorCredential[] credentials,
        EventTypeSelectedCalendar[] selectedCalendars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    );
}

public interface ICoreConnectorAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(ConnectorCredential credential, CancellationToken cancellationToken);
}

public sealed class ConfigurationCoreConnectorAccessTokenProvider(IConfiguration configuration) : ICoreConnectorAccessTokenProvider
{
    public Task<string?> GetAccessTokenAsync(ConnectorCredential credential, CancellationToken cancellationToken)
    {
        return Task.FromResult(configuration[$"Connectors:Core:AccessTokens:{credential.Id}"]);
    }
}

public sealed class GoogleCalendarCoreConnectorProvider(IHttpClientFactory httpClientFactory, ICoreConnectorAccessTokenProvider accessTokenProvider)
    : ICoreConnectorProvider
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public bool Supports(string integration)
    {
        return integration.Equals(CoreConnectorConstants.GoogleCalendar, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CalendarBusyWindow[]> GetBusyWindowsAsync(
        ConnectorCredential[] credentials,
        EventTypeSelectedCalendar[] selectedCalendars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    )
    {
        var busyWindows = new List<CalendarBusyWindow>();
        foreach (var credential in credentials.Where(credential => Supports(credential.Integration)))
        {
            var calendarIds = selectedCalendars
                .Where(calendar => calendar.CredentialId == credential.Id)
                .Where(calendar => Supports(calendar.Integration))
                .Select(calendar => calendar.ExternalId.Trim())
                .Where(externalId => !string.IsNullOrWhiteSpace(externalId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (calendarIds.Length == 0) continue;

            busyWindows.AddRange(await FetchBusyWindowsAsync(credential, calendarIds, startTime, endTime, cancellationToken));
        }

        return busyWindows
            .OrderBy(window => window.StartTime)
            .ThenBy(window => window.EndTime)
            .ToArray();
    }

    private async Task<CalendarBusyWindow[]> FetchBusyWindowsAsync(
        ConnectorCredential credential,
        string[] calendarIds,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(credential, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new HttpRequestException($"Access token for connector credential '{credential.Id}' was not found.");
        }

        var httpClient = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/calendar/v3/freeBusy");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
            {
                timeMin = startTime.ToString("O"),
                timeMax = endTime.ToString("O"),
                items = calendarIds.Select(id => new { id }).ToArray()
            }
        );

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var freeBusyResponse = await response.Content.ReadFromJsonAsync<GoogleFreeBusyResponse>(JsonSerializerOptions, cancellationToken);
        return MapBusyWindows(freeBusyResponse);
    }

    private static CalendarBusyWindow[] MapBusyWindows(GoogleFreeBusyResponse? response)
    {
        if (response?.Calendars is null) return [];

        return response.Calendars
            .SelectMany(calendar => calendar.Value.Busy ?? [])
            .Select(busyTime =>
                DateTimeOffset.TryParse(busyTime.Start, out var startTime) &&
                DateTimeOffset.TryParse(busyTime.End, out var endTime) &&
                endTime > startTime
                    ? new CalendarBusyWindow(startTime, endTime)
                    : null
            )
            .OfType<CalendarBusyWindow>()
            .OrderBy(window => window.StartTime)
            .ThenBy(window => window.EndTime)
            .ToArray();
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record GoogleFreeBusyResponse(Dictionary<string, GoogleCalendarFreeBusy> Calendars);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record GoogleCalendarFreeBusy(GoogleBusyTime[]? Busy);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record GoogleBusyTime(string? Start, string? End);
}

public sealed class Office365CalendarCoreConnectorProvider(IHttpClientFactory httpClientFactory, ICoreConnectorAccessTokenProvider accessTokenProvider)
    : ICoreConnectorProvider
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public bool Supports(string integration)
    {
        return integration.Equals(CoreConnectorConstants.Office365Calendar, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CalendarBusyWindow[]> GetBusyWindowsAsync(
        ConnectorCredential[] credentials,
        EventTypeSelectedCalendar[] selectedCalendars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    )
    {
        var busyWindows = new List<CalendarBusyWindow>();
        foreach (var credential in credentials.Where(credential => Supports(credential.Integration)))
        {
            var calendarIds = selectedCalendars
                .Where(calendar => calendar.CredentialId == credential.Id)
                .Where(calendar => Supports(calendar.Integration))
                .Select(calendar => calendar.ExternalId.Trim())
                .Where(externalId => !string.IsNullOrWhiteSpace(externalId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (calendarIds.Length == 0) continue;

            busyWindows.AddRange(await FetchBusyWindowsAsync(credential, calendarIds, startTime, endTime, cancellationToken));
        }

        return busyWindows
            .OrderBy(window => window.StartTime)
            .ThenBy(window => window.EndTime)
            .ToArray();
    }

    private async Task<CalendarBusyWindow[]> FetchBusyWindowsAsync(
        ConnectorCredential credential,
        string[] calendarIds,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(credential, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new HttpRequestException($"Access token for connector credential '{credential.Id}' was not found.");
        }

        var httpClient = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/$batch");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
            {
                requests = calendarIds.Select((calendarId, index) => new
                    {
                        id = index.ToString(),
                        method = "GET",
                        url = CalendarViewUrl(calendarId, startTime, endTime)
                    }
                ).ToArray()
            }
        );

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var batchResponse = await response.Content.ReadFromJsonAsync<Office365BatchResponse>(JsonSerializerOptions, cancellationToken);
        return MapBusyWindows(batchResponse);
    }

    private static string CalendarViewUrl(string calendarId, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var start = Uri.EscapeDataString(startTime.ToString("O"));
        var end = Uri.EscapeDataString(endTime.ToString("O"));
        return $"/me/calendars/{calendarId}/calendarView?startDateTime={start}&endDateTime={end}&$select=showAs,start,end&$top=999";
    }

    private static CalendarBusyWindow[] MapBusyWindows(Office365BatchResponse? response)
    {
        if (response?.Responses is null) return [];

        return response.Responses
            .Where(batchResponse => batchResponse.Status is >= 200 and <= 299)
            .SelectMany(batchResponse => batchResponse.Body?.Value ?? [])
            .Where(item => !item.ShowAs.Equals("free", StringComparison.OrdinalIgnoreCase))
            .Select(item =>
                TryParseOfficeDateTime(item.Start, out var startTime) &&
                TryParseOfficeDateTime(item.End, out var endTime) &&
                endTime > startTime
                    ? new CalendarBusyWindow(startTime, endTime)
                    : null
            )
            .OfType<CalendarBusyWindow>()
            .OrderBy(window => window.StartTime)
            .ThenBy(window => window.EndTime)
            .ToArray();
    }

    private static bool TryParseOfficeDateTime(Office365DateTime? value, out DateTimeOffset dateTime)
    {
        dateTime = default;
        if (string.IsNullOrWhiteSpace(value?.DateTime)) return false;
        if (DateTimeOffset.TryParse(value.DateTime, out dateTime)) return true;
        if (!DateTime.TryParse(value.DateTime, out var unspecifiedDateTime)) return false;

        dateTime = new DateTimeOffset(DateTime.SpecifyKind(unspecifiedDateTime, DateTimeKind.Utc));
        return true;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record Office365BatchResponse(Office365BatchItem[]? Responses);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record Office365BatchItem(int Status, Office365CalendarViewBody? Body);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record Office365CalendarViewBody(Office365CalendarViewItem[]? Value);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record Office365CalendarViewItem(string ShowAs, Office365DateTime? Start, Office365DateTime? End);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record Office365DateTime(string? DateTime);
}

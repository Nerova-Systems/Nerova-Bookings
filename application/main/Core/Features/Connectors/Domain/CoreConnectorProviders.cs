using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
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

public interface ICoreCalendarConnectorProvider : ICoreConnectorProvider
{
    Task<BookingReference> CreateCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    );

    Task<BookingReference> UpdateCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        BookingReference existingReference,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    );

    Task DeleteCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        BookingReference existingReference,
        CancellationToken cancellationToken
    );
}

public interface ICoreConferencingConnectorProvider
{
    bool SupportsConferencing(string app);

    Task<BookingReference> CreateMeetingAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDefaultConferencing conferencing,
        CancellationToken cancellationToken
    );

    Task<BookingReference> UpdateMeetingAsync(
        ConnectorCredential credential,
        Booking booking,
        BookingReference existingReference,
        EventTypeDefaultConferencing conferencing,
        CancellationToken cancellationToken
    );

    Task DeleteMeetingAsync(
        ConnectorCredential credential,
        Booking booking,
        BookingReference existingReference,
        EventTypeDefaultConferencing conferencing,
        CancellationToken cancellationToken
    );
}

public interface ICoreConnectorAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(ConnectorCredential credential, CancellationToken cancellationToken);
}

public sealed class ConfigurationCoreConnectorAccessTokenProvider(IConfiguration? configuration = null) : ICoreConnectorAccessTokenProvider
{
    public Task<string?> GetAccessTokenAsync(ConnectorCredential credential, CancellationToken cancellationToken)
    {
        return Task.FromResult(configuration?[$"Connectors:Core:AccessTokens:{credential.Id}"]);
    }
}

public sealed class GoogleCalendarCoreConnectorProvider(IHttpClientFactory httpClientFactory, ICoreConnectorAccessTokenProvider accessTokenProvider)
    : ICoreCalendarConnectorProvider
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public bool Supports(string integration)
    {
        return integration.Equals(CoreConnectorConstants.GoogleCalendar, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<BookingReference> CreateCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        var calendarId = NormalizeCalendarId(destinationCalendar.ExternalId);
        using var request = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Post,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events?conferenceDataVersion=1&sendUpdates=none",
            cancellationToken
        );
        request.Content = JsonContent.Create(CreateGoogleEventPayload(booking, conferencing));
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return MapGoogleReference(
            await response.Content.ReadFromJsonAsync<GoogleCalendarEventResponse>(JsonSerializerOptions, cancellationToken),
            calendarId
        );
    }

    public async Task<BookingReference> UpdateCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        BookingReference existingReference,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        var calendarId = NormalizeCalendarId(destinationCalendar.ExternalId);
        using var request = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Put,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(existingReference.Uid)}?conferenceDataVersion=1&sendUpdates=none",
            cancellationToken
        );
        request.Content = JsonContent.Create(CreateGoogleEventPayload(booking, conferencing));
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return MapGoogleReference(
            await response.Content.ReadFromJsonAsync<GoogleCalendarEventResponse>(JsonSerializerOptions, cancellationToken),
            calendarId
        );
    }

    public async Task DeleteCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        BookingReference existingReference,
        CancellationToken cancellationToken
    )
    {
        var calendarId = NormalizeCalendarId(destinationCalendar.ExternalId);
        using var request = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Delete,
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(existingReference.Uid)}?sendUpdates=none",
            cancellationToken
        );
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return;
        response.EnsureSuccessStatusCode();
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

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        ConnectorCredential credential,
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(credential, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new HttpRequestException($"Access token for connector credential '{credential.Id}' was not found.");
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static object CreateGoogleEventPayload(Booking booking, EventTypeDefaultConferencing? conferencing)
    {
        var basePayload = new Dictionary<string, object?>
        {
            ["summary"] = booking.Title,
            ["description"] = booking.Description,
            ["start"] = new { dateTime = booking.StartTime.ToString("O"), timeZone = booking.TimeZone },
            ["end"] = new { dateTime = booking.EndTime.ToString("O"), timeZone = booking.TimeZone },
            ["attendees"] = booking.Attendees.Select(attendee => new { email = attendee.Email, displayName = attendee.Name }).ToArray()
        };

        if (!string.IsNullOrWhiteSpace(booking.LocationValue))
        {
            basePayload["location"] = booking.LocationValue;
        }

        if (conferencing?.App.Equals(CoreConnectorConstants.GoogleMeet, StringComparison.OrdinalIgnoreCase) == true)
        {
            basePayload["conferenceData"] = new
            {
                createRequest = new
                {
                    requestId = booking.Id.Value
                }
            };
        }

        return basePayload;
    }

    private static BookingReference MapGoogleReference(GoogleCalendarEventResponse? response, string calendarId)
    {
        if (string.IsNullOrWhiteSpace(response?.Id))
        {
            throw new JsonException("Google Calendar event response did not include an event id.");
        }

        return new BookingReference(
            CoreConnectorConstants.GoogleCalendar,
            response.Id,
            string.IsNullOrWhiteSpace(response.HangoutLink) ? null : response.Id,
            null,
            string.IsNullOrWhiteSpace(response.HangoutLink) ? null : response.HangoutLink,
            calendarId,
            false
        );
    }

    private static string NormalizeCalendarId(string calendarId)
    {
        return string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId.Trim();
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

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record GoogleCalendarEventResponse(
        string? Id,
        [property: JsonPropertyName("iCalUID")]
        string? CalUid,
        string? HangoutLink
    );
}

public sealed class Office365CalendarCoreConnectorProvider(IHttpClientFactory httpClientFactory, ICoreConnectorAccessTokenProvider accessTokenProvider)
    : ICoreCalendarConnectorProvider
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public bool Supports(string integration)
    {
        return integration.Equals(CoreConnectorConstants.Office365Calendar, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<BookingReference> CreateCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        var calendarId = NormalizeCalendarId(destinationCalendar.ExternalId);
        using var request = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/me/calendars/{Uri.EscapeDataString(calendarId)}/events",
            cancellationToken
        );
        request.Content = JsonContent.Create(CreateOffice365EventPayload(booking, conferencing));
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return MapOffice365Reference(
            await response.Content.ReadFromJsonAsync<Office365CalendarEventResponse>(JsonSerializerOptions, cancellationToken),
            calendarId
        );
    }

    public async Task<BookingReference> UpdateCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        BookingReference existingReference,
        EventTypeDefaultConferencing? conferencing,
        CancellationToken cancellationToken
    )
    {
        var calendarId = NormalizeCalendarId(destinationCalendar.ExternalId);
        using var request = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Patch,
            $"https://graph.microsoft.com/v1.0/me/calendar/events/{Uri.EscapeDataString(existingReference.Uid)}",
            cancellationToken
        );
        request.Content = JsonContent.Create(CreateOffice365EventPayload(booking, conferencing));
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return MapOffice365Reference(
            await response.Content.ReadFromJsonAsync<Office365CalendarEventResponse>(JsonSerializerOptions, cancellationToken),
            calendarId
        );
    }

    public async Task DeleteCalendarEventAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDestinationCalendar destinationCalendar,
        BookingReference existingReference,
        CancellationToken cancellationToken
    )
    {
        using var request = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Delete,
            $"https://graph.microsoft.com/v1.0/me/calendar/events/{Uri.EscapeDataString(existingReference.Uid)}",
            cancellationToken
        );
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return;
        response.EnsureSuccessStatusCode();
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

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        ConnectorCredential credential,
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(credential, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new HttpRequestException($"Access token for connector credential '{credential.Id}' was not found.");
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static object CreateOffice365EventPayload(Booking booking, EventTypeDefaultConferencing? conferencing)
    {
        var payload = new Dictionary<string, object?>
        {
            ["subject"] = booking.Title,
            ["body"] = new { contentType = "HTML", content = booking.Description ?? string.Empty },
            ["start"] = new { dateTime = booking.StartTime.ToString("O"), timeZone = booking.TimeZone },
            ["end"] = new { dateTime = booking.EndTime.ToString("O"), timeZone = booking.TimeZone },
            ["attendees"] = booking.Attendees.Select(attendee => new
                {
                    emailAddress = new { address = attendee.Email, name = attendee.Name },
                    type = "required"
                }
            ).ToArray(),
            ["location"] = new { displayName = booking.LocationValue ?? string.Empty }
        };

        if (conferencing?.App.Equals(CoreConnectorConstants.Office365Video, StringComparison.OrdinalIgnoreCase) == true)
        {
            payload["isOnlineMeeting"] = true;
            payload["onlineMeetingProvider"] = "teamsForBusiness";
        }

        return payload;
    }

    private static BookingReference MapOffice365Reference(Office365CalendarEventResponse? response, string calendarId)
    {
        if (string.IsNullOrWhiteSpace(response?.Id))
        {
            throw new JsonException("Office 365 Calendar event response did not include an event id.");
        }

        return new BookingReference(
            CoreConnectorConstants.Office365Calendar,
            response.Id,
            string.IsNullOrWhiteSpace(response.OnlineMeeting?.JoinUrl) ? null : response.Id,
            null,
            string.IsNullOrWhiteSpace(response.OnlineMeeting?.JoinUrl) ? null : response.OnlineMeeting.JoinUrl,
            calendarId,
            false
        );
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

    private static string NormalizeCalendarId(string calendarId)
    {
        return string.IsNullOrWhiteSpace(calendarId) ? "calendar" : calendarId.Trim();
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

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record Office365CalendarEventResponse(
        string? Id,
        [property: JsonPropertyName("iCalUId")]
        string? CalUId,
        Office365OnlineMeeting? OnlineMeeting
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record Office365OnlineMeeting(string? JoinUrl);
}

public sealed class ZoomCoreConnectorProvider(IHttpClientFactory httpClientFactory, ICoreConnectorAccessTokenProvider accessTokenProvider)
    : ICoreConferencingConnectorProvider
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    public bool SupportsConferencing(string app)
    {
        return app.Equals(CoreConnectorConstants.ZoomVideo, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<BookingReference> CreateMeetingAsync(
        ConnectorCredential credential,
        Booking booking,
        EventTypeDefaultConferencing conferencing,
        CancellationToken cancellationToken
    )
    {
        using var request = await CreateAuthorizedRequestAsync(credential, HttpMethod.Post, "https://api.zoom.us/v2/users/me/meetings", cancellationToken);
        request.Content = JsonContent.Create(CreateZoomMeetingPayload(booking));
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return MapZoomReference(await response.Content.ReadFromJsonAsync<ZoomMeetingResponse>(JsonSerializerOptions, cancellationToken));
    }

    public async Task<BookingReference> UpdateMeetingAsync(
        ConnectorCredential credential,
        Booking booking,
        BookingReference existingReference,
        EventTypeDefaultConferencing conferencing,
        CancellationToken cancellationToken
    )
    {
        var meetingId = string.IsNullOrWhiteSpace(existingReference.MeetingId) ? existingReference.Uid : existingReference.MeetingId;
        using var updateRequest = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Patch,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meetingId)}",
            cancellationToken
        );
        updateRequest.Content = JsonContent.Create(CreateZoomMeetingPayload(booking));
        using var updateResponse = await httpClientFactory.CreateClient().SendAsync(updateRequest, cancellationToken);
        updateResponse.EnsureSuccessStatusCode();

        using var getRequest = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Get,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meetingId)}",
            cancellationToken
        );
        using var getResponse = await httpClientFactory.CreateClient().SendAsync(getRequest, cancellationToken);
        getResponse.EnsureSuccessStatusCode();
        return MapZoomReference(await getResponse.Content.ReadFromJsonAsync<ZoomMeetingResponse>(JsonSerializerOptions, cancellationToken));
    }

    public async Task DeleteMeetingAsync(
        ConnectorCredential credential,
        Booking booking,
        BookingReference existingReference,
        EventTypeDefaultConferencing conferencing,
        CancellationToken cancellationToken
    )
    {
        var meetingId = string.IsNullOrWhiteSpace(existingReference.MeetingId) ? existingReference.Uid : existingReference.MeetingId;
        using var request = await CreateAuthorizedRequestAsync(
            credential,
            HttpMethod.Delete,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meetingId)}",
            cancellationToken
        );
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return;
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        ConnectorCredential credential,
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken
    )
    {
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(credential, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new HttpRequestException($"Access token for connector credential '{credential.Id}' was not found.");
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static object CreateZoomMeetingPayload(Booking booking)
    {
        return new
        {
            topic = booking.Title,
            agenda = booking.Description,
            type = 2,
            start_time = booking.StartTime.ToString("O"),
            duration = (int)(booking.EndTime - booking.StartTime).TotalMinutes,
            timezone = booking.TimeZone
        };
    }

    private static BookingReference MapZoomReference(ZoomMeetingResponse? response)
    {
        if (response?.Id is null)
        {
            throw new JsonException("Zoom meeting response did not include a meeting id.");
        }

        var meetingId = response.Id.Value.ToString();
        return new BookingReference(
            CoreConnectorConstants.ZoomVideo,
            meetingId,
            meetingId,
            string.IsNullOrWhiteSpace(response.Password) ? null : response.Password,
            string.IsNullOrWhiteSpace(response.JoinUrl) ? null : response.JoinUrl,
            null,
            false
        );
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ZoomMeetingResponse(
        long? Id,
        string? Password,
        [property: JsonPropertyName("join_url")]
        string? JoinUrl
    );
}

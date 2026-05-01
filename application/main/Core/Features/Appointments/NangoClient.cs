using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Main.Features.Appointments;

public interface INangoClient
{
    Task<NangoConnectSession> CreateConnectSessionAsync(NangoConnectSessionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<NangoConnection>> ListConnectionsAsync(string integrationKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<NangoCalendar>> ListCalendarsAsync(string integrationKey, string connectionId, CancellationToken cancellationToken);
    Task<NangoCalendarEvent> CreateCalendarEventAsync(string integrationKey, string connectionId, NangoCalendarEventRequest request, CancellationToken cancellationToken);
    Task<NangoCalendarEvent> UpdateCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, NangoCalendarEventRequest request, CancellationToken cancellationToken);
    Task DeleteCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, CancellationToken cancellationToken);
}

public sealed class NangoClient(IHttpClientFactory httpClientFactory) : INangoClient
{
    private static readonly Uri DefaultBaseUrl = new("https://api.nango.dev");

    public async Task<NangoConnectSession> CreateConnectSessionAsync(NangoConnectSessionRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(GetBaseUrl(), "/connect/sessions"))
        {
            Content = JsonContent.Create(new
            {
                tags = request.Tags,
                allowed_integrations = request.AllowedIntegrations
            })
        };
        AddAuthorization(message);
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var data = ReadDataObject(json);
        var connectLink = ReadString(data, "connect_link", "connectLink");
        if (string.IsNullOrWhiteSpace(connectLink))
        {
            throw new InvalidOperationException("Nango did not return a connect link.");
        }

        var expiresAt = ReadDateTimeOffset(data, "expires_at", "expiresAt") ?? DateTimeOffset.UtcNow.AddMinutes(30);
        return new NangoConnectSession(connectLink, expiresAt);
    }

    public async Task<IReadOnlyList<NangoConnection>> ListConnectionsAsync(string integrationKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(GetBaseUrl(), $"/connections?integrationId={Uri.EscapeDataString(integrationKey)}"));
        AddAuthorization(request);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var connections = ReadConnectionsArray(json);
        return connections
            .EnumerateArray()
            .Select(connection => new NangoConnection(
                ReadString(connection, "connection_id", "connectionId", "id") ?? string.Empty,
                ReadString(connection, "end_user_id", "endUserId") ?? string.Empty,
                ReadDateTimeOffset(connection, "updated_at", "updatedAt", "created_at", "createdAt", "last_fetched_at", "lastFetchedAt")
            ))
            .Where(connection => !string.IsNullOrWhiteSpace(connection.ConnectionId))
            .ToList();
    }

    public async Task<IReadOnlyList<NangoCalendar>> ListCalendarsAsync(string integrationKey, string connectionId, CancellationToken cancellationToken)
    {
        using var request = CreateProxyRequest(HttpMethod.Get, integrationKey, connectionId, "users/me/calendarList");
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var items = json.TryGetProperty("items", out var calendars) && calendars.ValueKind == JsonValueKind.Array
            ? calendars
            : JsonDocument.Parse("[]").RootElement;

        return items.EnumerateArray()
            .Select(calendar => new NangoCalendar(
                ReadString(calendar, "id") ?? string.Empty,
                ReadString(calendar, "summary") ?? ReadString(calendar, "id") ?? string.Empty,
                calendar.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True,
                ReadString(calendar, "accessRole") is "owner" or "writer"
            ))
            .Where(calendar => !string.IsNullOrWhiteSpace(calendar.Id))
            .ToList();
    }

    public async Task<NangoCalendarEvent> CreateCalendarEventAsync(string integrationKey, string connectionId, NangoCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"calendars/{Uri.EscapeDataString(request.CalendarId)}/events?conferenceDataVersion=1&sendUpdates=all";
        using var message = CreateProxyRequest(HttpMethod.Post, integrationKey, connectionId, endpoint);
        message.Content = JsonContent.Create(ToGoogleEventPayload(request, includeConferenceData: true));
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadCalendarEventAsync(response, cancellationToken);
    }

    public async Task<NangoCalendarEvent> UpdateCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, NangoCalendarEventRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}?conferenceDataVersion=1&sendUpdates=all";
        using var message = CreateProxyRequest(HttpMethod.Patch, integrationKey, connectionId, endpoint);
        message.Content = JsonContent.Create(ToGoogleEventPayload(request, includeConferenceData: false));
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadCalendarEventAsync(response, cancellationToken);
    }

    public async Task DeleteCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, CancellationToken cancellationToken)
    {
        var endpoint = $"calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}?sendUpdates=all";
        using var request = CreateProxyRequest(HttpMethod.Delete, integrationKey, connectionId, endpoint);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Gone) return;
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static void AddAuthorization(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetSecret());
    }

    private static HttpRequestMessage CreateProxyRequest(HttpMethod method, string integrationKey, string connectionId, string endpoint)
    {
        var request = new HttpRequestMessage(method, new Uri(GetBaseUrl(), $"/proxy/{endpoint.TrimStart('/')}"));
        AddAuthorization(request);
        request.Headers.Add("Provider-Config-Key", integrationKey);
        request.Headers.Add("Connection-Id", connectionId);
        request.Headers.Add("Retries", "2");
        return request;
    }

    private static object ToGoogleEventPayload(NangoCalendarEventRequest request, bool includeConferenceData)
    {
        var payload = new Dictionary<string, object?>
        {
            ["summary"] = request.Summary,
            ["description"] = request.Description,
            ["location"] = request.Location,
            ["start"] = new { dateTime = request.StartAt.ToString("O"), timeZone = request.TimeZone },
            ["end"] = new { dateTime = request.EndAt.ToString("O"), timeZone = request.TimeZone },
            ["attendees"] = request.Attendees.Select(attendee => new { email = attendee.Email, displayName = attendee.Name }).ToArray()
        };
        if (includeConferenceData)
        {
            payload["conferenceData"] = new
            {
                createRequest = new
                {
                    requestId = request.ConferenceRequestId,
                    conferenceSolutionKey = new { type = "hangoutsMeet" }
                }
            };
        }
        return payload;
    }

    private static async Task<NangoCalendarEvent> ReadCalendarEventAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var eventId = ReadString(json, "id") ?? string.Empty;
        var meetUrl = ReadString(json, "hangoutLink") ?? ReadMeetUrl(json);
        return new NangoCalendarEvent(eventId, meetUrl);
    }

    private static string? ReadMeetUrl(JsonElement json)
    {
        if (!json.TryGetProperty("conferenceData", out var conferenceData) ||
            !conferenceData.TryGetProperty("entryPoints", out var entryPoints) ||
            entryPoints.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return entryPoints.EnumerateArray()
            .Where(entry => ReadString(entry, "entryPointType") == "video")
            .Select(entry => ReadString(entry, "uri"))
            .FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri));
    }

    private static Uri GetBaseUrl()
    {
        var configured = Environment.GetEnvironmentVariable("NANGO_SERVER_URL");
        return Uri.TryCreate(configured, UriKind.Absolute, out var uri) ? uri : DefaultBaseUrl;
    }

    private static string GetSecret()
    {
        var secret = Environment.GetEnvironmentVariable("NANGO_TOOLBOX_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = Environment.GetEnvironmentVariable("NANGO_SECRET_KEY");
        }

        return string.IsNullOrWhiteSpace(secret)
            ? throw new NangoConfigurationException("Nango is not configured. Set NANGO_TOOLBOX_SECRET_KEY or NANGO_SECRET_KEY.")
            : secret;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? "Nango request failed." : body);
    }

    private static JsonElement ReadDataObject(JsonElement json)
    {
        return json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object ? data : json;
    }

    private static JsonElement ReadConnectionsArray(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Array) return json;
        if (json.TryGetProperty("connections", out var connections) && connections.ValueKind == JsonValueKind.Array) return connections;
        if (json.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array) return data;
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("connections", out connections) && connections.ValueKind == JsonValueKind.Array) return connections;
        }

        return JsonDocument.Parse("[]").RootElement;
    }

    private static string? ReadString(JsonElement json, params string[] names)
    {
        foreach (var name in names)
        {
            if (json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement json, params string[] names)
    {
        var value = ReadString(json, names);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}

public sealed class NangoConfigurationException(string message) : InvalidOperationException(message);

public sealed record NangoConnectSessionRequest(string IntegrationKey, IReadOnlyList<string> AllowedIntegrations, IReadOnlyDictionary<string, string> Tags);
public sealed record NangoConnectSession(string ConnectLink, DateTimeOffset ExpiresAt);
public sealed record NangoConnection(string ConnectionId, string EndUserId, DateTimeOffset? LastSyncedAt);
public sealed record NangoCalendar(string Id, string Name, bool IsPrimary, bool CanWrite);
public sealed record NangoCalendarAttendee(string Name, string Email);
public sealed record NangoCalendarEventRequest(
    string AppointmentId,
    string CalendarId,
    string Summary,
    string Description,
    string Location,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string TimeZone,
    string ConferenceRequestId,
    IReadOnlyList<NangoCalendarAttendee> Attendees
);
public sealed record NangoCalendarEvent(string EventId, string? MeetUrl);

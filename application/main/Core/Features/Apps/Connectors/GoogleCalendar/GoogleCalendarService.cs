using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.Extensions.Options;

namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     Per-credential Google Calendar API client. Handles free-busy lookups and the booking
///     event lifecycle (create / update / cancel). Transparently refreshes the OAuth access
///     token on a 401 response and persists the rotated blob back via the supplied callback so
///     subsequent calls reuse the new token without a second refresh round-trip.
///     <para>
///         Construct one instance per credential via <see cref="GoogleCalendarServiceFactory" /> —
///         the service is intentionally stateful (it caches the current token blob) and must not
///         be registered as a long-lived singleton.
///     </para>
/// </summary>
public sealed class GoogleCalendarService(
    HttpClient httpClient,
    GoogleCalendarOptions options,
    GoogleCredentialBlob initialBlob,
    Func<string, CancellationToken, Task> persistRefreshedBlobAsync,
    TimeProvider timeProvider
)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Snapshot of the in-memory credential — exposed for tests and the busy-time provider.</summary>
    public GoogleCredentialBlob CurrentBlob { get; private set; } = initialBlob;

    // ─── Free-busy ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ExternalBusyTime>> GetBusyTimesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken
    )
    {
        var payload = new
        {
            timeMin = from.ToUniversalTime().ToString("o"),
            timeMax = to.ToUniversalTime().ToString("o"),
            items = new[] { new { id = "primary" } }
        };

        using var response = await SendWithRefreshAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{options.ApiBaseUrl}/freeBusy")
                {
                    Content = JsonContent.Create(payload, options: JsonOptions)
                };
                return request;
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "freeBusy", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        var busy = root?["calendars"]?["primary"]?["busy"]?.AsArray();
        if (busy is null) return [];

        var result = new List<ExternalBusyTime>(busy.Count);
        foreach (var entry in busy)
        {
            var start = entry?["start"]?.GetValue<string>();
            var end = entry?["end"]?.GetValue<string>();
            if (start is null || end is null) continue;
            result.Add(new ExternalBusyTime(DateTimeOffset.Parse(start), DateTimeOffset.Parse(end)));
        }

        return result;
    }

    // ─── Event lifecycle ────────────────────────────────────────────────────

    public async Task<string> CreateEventAsync(BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildEventBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{options.ApiBaseUrl}/calendars/primary/events")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "events.insert", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        return json?["id"]?.GetValue<string>()
               ?? throw new InvalidOperationException("Google events.insert response missing 'id'.");
    }

    /// <summary>
    ///     Creates a Google Calendar event with an attached Google Meet conference (via
    ///     <c>conferenceData.createRequest</c> + <c>conferenceDataVersion=1</c>) and returns the
    ///     event id and the generated <c>hangoutLink</c>. Used by the Google Meet connector,
    ///     which reuses the Google Calendar credential rather than holding its own.
    /// </summary>
    public async Task<(string EventId, string JoinUrl)> CreateEventWithMeetLinkAsync(
        BookingEvent input,
        CancellationToken cancellationToken
    )
    {
        var body = BuildEventBodyWithMeet(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{options.ApiBaseUrl}/calendars/primary/events?conferenceDataVersion=1")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "events.insert", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
                   ?? throw new InvalidOperationException("Google events.insert response was empty.");
        var eventId = json["id"]?.GetValue<string>()
                      ?? throw new InvalidOperationException("Google events.insert response missing 'id'.");
        var joinUrl = json["hangoutLink"]?.GetValue<string>()
                      ?? json["conferenceData"]?["entryPoints"]?.AsArray().FirstOrDefault()?["uri"]?.GetValue<string>()
                      ?? throw new InvalidOperationException("Google events.insert response missing 'hangoutLink' / conferenceData entry point — was conferenceData.createRequest accepted?");
        return (eventId, joinUrl);
    }

    public async Task UpdateEventAsync(string externalEventId, BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildEventBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{options.ApiBaseUrl}/calendars/primary/events/{Uri.EscapeDataString(externalEventId)}")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "events.update", cancellationToken);
    }

    public async Task CancelEventAsync(string externalEventId, CancellationToken cancellationToken)
    {
        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"{options.ApiBaseUrl}/calendars/primary/events/{Uri.EscapeDataString(externalEventId)}"),
            cancellationToken
        );

        // 410 Gone is acceptable — the event was already deleted upstream.
        if (response.StatusCode == HttpStatusCode.Gone || response.StatusCode == HttpStatusCode.NotFound) return;
        await EnsureSuccessAsync(response, "events.delete", cancellationToken);
    }

    // ─── Internals ──────────────────────────────────────────────────────────

    internal static object BuildEventBody(BookingEvent input)
    {
        var attendees = BuildAttendees(input);

        return new
        {
            summary = input.Title,
            description = input.Description,
            location = input.Location,
            iCalUID = input.CalUid,
            start = new { dateTime = input.StartTime.ToUniversalTime().ToString("o"), timeZone = input.TimeZone },
            end = new { dateTime = input.EndTime.ToUniversalTime().ToString("o"), timeZone = input.TimeZone },
            organizer = new { email = input.OrganizerEmail, displayName = input.OrganizerName },
            attendees
        };
    }

    internal static object BuildEventBodyWithMeet(BookingEvent input)
    {
        var attendees = BuildAttendees(input);
        // Per Google Calendar API: a unique requestId scopes the createRequest so retried inserts
        // don't generate multiple conferences. We use the iCalUid when supplied (stable for a
        // booking) and fall back to a fresh GUID.
        var requestId = string.IsNullOrEmpty(input.CalUid) ? Guid.NewGuid().ToString("N") : input.CalUid;

        return new
        {
            summary = input.Title,
            description = input.Description,
            location = input.Location,
            iCalUID = input.CalUid,
            start = new { dateTime = input.StartTime.ToUniversalTime().ToString("o"), timeZone = input.TimeZone },
            end = new { dateTime = input.EndTime.ToUniversalTime().ToString("o"), timeZone = input.TimeZone },
            organizer = new { email = input.OrganizerEmail, displayName = input.OrganizerName },
            attendees,
            conferenceData = new
            {
                createRequest = new
                {
                    requestId,
                    conferenceSolutionKey = new { type = "hangoutsMeet" }
                }
            }
        };
    }

    private static object[] BuildAttendees(BookingEvent input)
    {
        return input.Attendees
            .Select(attendee => attendee.Name is null
                ? (object)new { email = attendee.Email }
                : new { email = attendee.Email, displayName = attendee.Name }
            )
            .ToArray();
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        Func<HttpRequestMessage> buildRequest,
        CancellationToken cancellationToken
    )
    {
        var first = buildRequest();
        GoogleCalendarInstaller.ApplyBearer(first, CurrentBlob.AccessToken);
        var response = await httpClient.SendAsync(first, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        await RefreshAccessTokenAsync(cancellationToken);

        var retry = buildRequest();
        GoogleCalendarInstaller.ApplyBearer(retry, CurrentBlob.AccessToken);
        return await httpClient.SendAsync(retry, cancellationToken);
    }

    private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["refresh_token"] = CurrentBlob.RefreshToken,
                ["grant_type"] = "refresh_token"
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenUrl);
        request.Content = content;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google token refresh failed with status {(int)response.StatusCode}: {body}"
            );
        }

        var token = JsonSerializer.Deserialize<GoogleTokenResponse>(body, GoogleTokenResponse.JsonOptions)
                    ?? throw new InvalidOperationException("Google token refresh response was empty.");
        if (string.IsNullOrEmpty(token.AccessToken)) throw new InvalidOperationException("Google token refresh response missing access_token.");

        CurrentBlob = new GoogleCredentialBlob(
            token.AccessToken,
            // Refresh responses normally omit refresh_token — keep the existing one.
            string.IsNullOrEmpty(token.RefreshToken) ? CurrentBlob.RefreshToken : token.RefreshToken,
            timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 60)),
            token.Scope ?? CurrentBlob.Scope
        );

        await persistRefreshedBlobAsync(CurrentBlob.ToJson(), cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Google {operation} failed with status {(int)response.StatusCode}: {body}"
        );
    }
}

/// <summary>
///     Builds a <see cref="GoogleCalendarService" /> for a stored <see cref="Credential" />,
///     handling decryption of the JSON blob and wiring the refresh-token persistence callback
///     back through <see cref="ICredentialRepository" />.
/// </summary>
public sealed class GoogleCalendarServiceFactory(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<GoogleCalendarOptions> options,
    CredentialProtector protector,
    ICredentialRepository credentialRepository,
    TimeProvider timeProvider
)
{
    public GoogleCalendarService Create(Credential credential)
    {
        if (credential.AppSlug != GoogleCalendarSlug.Slug)
        {
            throw new ArgumentException(
                $"Credential is for app '{credential.AppSlug.Value}', not '{GoogleCalendarSlug.Value}'.",
                nameof(credential)
            );
        }

        var blob = GoogleCredentialBlob.FromJson(protector.Unprotect(credential.EncryptedKey));
        var client = httpClientFactory.CreateClient(GoogleCalendarSlug.HttpClientName);

        return new GoogleCalendarService(
            client,
            options.CurrentValue,
            blob,
            async (newJson, ct) =>
            {
                var encrypted = protector.Protect(newJson);
                credential.UpdateKey(encrypted);
                credentialRepository.Update(credential);
                await Task.CompletedTask;
                _ = ct;
            },
            timeProvider
        );
    }
}

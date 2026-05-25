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
public sealed class GoogleCalendarService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GoogleCalendarOptions _options;
    private readonly Func<string, CancellationToken, Task> _persistRefreshedBlobAsync;
    private readonly TimeProvider _timeProvider;
    private GoogleCredentialBlob _blob;

    public GoogleCalendarService(
        HttpClient httpClient,
        GoogleCalendarOptions options,
        GoogleCredentialBlob initialBlob,
        Func<string, CancellationToken, Task> persistRefreshedBlobAsync,
        TimeProvider timeProvider
    )
    {
        _httpClient = httpClient;
        _options = options;
        _blob = initialBlob;
        _persistRefreshedBlobAsync = persistRefreshedBlobAsync;
        _timeProvider = timeProvider;
    }

    /// <summary>Snapshot of the in-memory credential — exposed for tests and the busy-time provider.</summary>
    public GoogleCredentialBlob CurrentBlob => _blob;

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
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl}/freeBusy")
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
            () => new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl}/calendars/primary/events")
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

    public async Task UpdateEventAsync(string externalEventId, BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildEventBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Put, $"{_options.ApiBaseUrl}/calendars/primary/events/{Uri.EscapeDataString(externalEventId)}")
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
            () => new HttpRequestMessage(HttpMethod.Delete, $"{_options.ApiBaseUrl}/calendars/primary/events/{Uri.EscapeDataString(externalEventId)}"),
            cancellationToken
        );

        // 410 Gone is acceptable — the event was already deleted upstream.
        if (response.StatusCode == HttpStatusCode.Gone || response.StatusCode == HttpStatusCode.NotFound) return;
        await EnsureSuccessAsync(response, "events.delete", cancellationToken);
    }

    // ─── Internals ──────────────────────────────────────────────────────────

    internal static object BuildEventBody(BookingEvent input)
    {
        var attendees = input.Attendees
            .Select(attendee => attendee.Name is null
                ? (object)new { email = attendee.Email }
                : new { email = attendee.Email, displayName = attendee.Name }
            )
            .ToArray();

        return new
        {
            summary = input.Title,
            description = input.Description,
            location = input.Location,
            iCalUID = input.ICalUid,
            start = new { dateTime = input.StartTime.ToUniversalTime().ToString("o"), timeZone = input.TimeZone },
            end = new { dateTime = input.EndTime.ToUniversalTime().ToString("o"), timeZone = input.TimeZone },
            organizer = new { email = input.OrganizerEmail, displayName = input.OrganizerName },
            attendees
        };
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        Func<HttpRequestMessage> buildRequest,
        CancellationToken cancellationToken
    )
    {
        var first = buildRequest();
        GoogleCalendarInstaller.ApplyBearer(first, _blob.AccessToken);
        var response = await _httpClient.SendAsync(first, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        await RefreshAccessTokenAsync(cancellationToken);

        var retry = buildRequest();
        GoogleCalendarInstaller.ApplyBearer(retry, _blob.AccessToken);
        return await _httpClient.SendAsync(retry, cancellationToken);
    }

    private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["refresh_token"] = _blob.RefreshToken,
                    ["grant_type"] = "refresh_token"
                }
            )
        };
        using var response = await _httpClient.SendAsync(request, cancellationToken);
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

        _blob = new GoogleCredentialBlob(
            token.AccessToken,
            // Refresh responses normally omit refresh_token — keep the existing one.
            string.IsNullOrEmpty(token.RefreshToken) ? _blob.RefreshToken : token.RefreshToken,
            _timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 60)),
            token.Scope ?? _blob.Scope
        );

        await _persistRefreshedBlobAsync(_blob.ToJson(), cancellationToken);
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

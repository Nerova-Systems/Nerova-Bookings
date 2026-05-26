using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.Extensions.Options;

namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>
///     Per-credential Microsoft Graph client for Outlook/Office 365 Calendar. Handles
///     free-busy lookups via <c>/me/calendar/getSchedule</c> and the booking event lifecycle
///     (create / update / cancel). Transparently refreshes the OAuth access token on a 401
///     response and persists the rotated blob back via the supplied callback so subsequent
///     calls reuse the new token without a second refresh round-trip.
///     <para>
///         Construct one instance per credential via <see cref="Office365CalendarServiceFactory" /> —
///         the service is intentionally stateful (it caches the current token blob and the
///         lazily-resolved user principal name) and must not be registered as a long-lived
///         singleton.
///     </para>
/// </summary>
public sealed class Office365CalendarService(
    HttpClient httpClient,
    Office365CalendarOptions options,
    Office365CredentialBlob initialBlob,
    Func<string, CancellationToken, Task> persistRefreshedBlobAsync,
    TimeProvider timeProvider
)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Snapshot of the in-memory credential — exposed for tests and the busy-time provider.</summary>
    public Office365CredentialBlob CurrentBlob { get; private set; } = initialBlob;

    // ─── Free-busy ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ExternalBusyTime>> GetBusyTimesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken
    )
    {
        var upn = await EnsureUserPrincipalNameAsync(cancellationToken);

        var payload = new
        {
            schedules = new[] { upn },
            // Microsoft expects ISO-8601 without a trailing 'Z' when the timeZone field is
            // provided alongside; UTC + "UTC" timeZone is the simplest interoperable shape.
            startTime = new { dateTime = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            endTime = new { dateTime = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            availabilityViewInterval = 30
        };

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{options.ApiBaseUrl}/me/calendar/getSchedule")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "getSchedule", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        var value = root?["value"]?.AsArray();
        if (value is null || value.Count == 0) return [];

        var result = new List<ExternalBusyTime>();
        foreach (var schedule in value)
        {
            var items = schedule?["scheduleItems"]?.AsArray();
            if (items is null) continue;
            foreach (var item in items)
            {
                var status = item?["status"]?.GetValue<string>();
                // "free" and "workingElsewhere" should not block a slot; everything else
                // (busy, tentative, oof, unknown) counts as unavailable.
                if (string.Equals(status, "free", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(status, "workingElsewhere", StringComparison.OrdinalIgnoreCase)) continue;

                var start = ReadGraphDateTime(item?["start"]);
                var end = ReadGraphDateTime(item?["end"]);
                if (start is null || end is null) continue;
                result.Add(new ExternalBusyTime(start.Value, end.Value));
            }
        }

        return result;
    }

    // ─── Event lifecycle ────────────────────────────────────────────────────

    public async Task<string> CreateEventAsync(BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildEventBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{options.ApiBaseUrl}/me/calendar/events")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "events.create", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var json = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        return json?["id"]?.GetValue<string>()
               ?? throw new InvalidOperationException("Microsoft Graph events.create response missing 'id'.");
    }

    public async Task UpdateEventAsync(string externalEventId, BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildEventBody(input);

        // Microsoft Graph uses PATCH (partial update) for events.update — sending PUT
        // returns 405.
        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Patch, $"{options.ApiBaseUrl}/me/calendar/events/{Uri.EscapeDataString(externalEventId)}")
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
            () => new HttpRequestMessage(HttpMethod.Delete, $"{options.ApiBaseUrl}/me/calendar/events/{Uri.EscapeDataString(externalEventId)}"),
            cancellationToken
        );

        // 404 is acceptable — the event was already deleted upstream.
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) return;
        await EnsureSuccessAsync(response, "events.delete", cancellationToken);
    }

    // ─── Online meeting lifecycle (MS Teams) ────────────────────────────────

    /// <summary>
    ///     POSTs to <c>/me/onlineMeetings</c> to create a Microsoft Teams meeting and returns
    ///     the meeting <c>id</c> + <c>joinUrl</c>. The Graph API only accepts <c>subject</c> +
    ///     <c>startDateTime</c>/<c>endDateTime</c> — attendees and description are surfaced via
    ///     the calendar event the booking layer creates separately, not on the meeting itself.
    /// </summary>
    public async Task<(string Id, string JoinUrl)> CreateOnlineMeetingAsync(BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildOnlineMeetingBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{options.ApiBaseUrl}/me/onlineMeetings")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "onlineMeetings.create", cancellationToken);
        return await ParseOnlineMeetingAsync(response, cancellationToken);
    }

    public async Task<(string Id, string JoinUrl)> UpdateOnlineMeetingAsync(string meetingId, BookingEvent input, CancellationToken cancellationToken)
    {
        // Graph uses PATCH for onlineMeeting updates — sending PUT returns 405. The response
        // body is the updated meeting (same shape as create), so we re-parse the joinUrl in
        // case it ever changes upstream (today it is stable but the contract allows rotation).
        var body = BuildOnlineMeetingBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Patch, $"{options.ApiBaseUrl}/me/onlineMeetings/{Uri.EscapeDataString(meetingId)}")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "onlineMeetings.update", cancellationToken);
        return await ParseOnlineMeetingAsync(response, cancellationToken);
    }

    public async Task CancelOnlineMeetingAsync(string meetingId, CancellationToken cancellationToken)
    {
        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"{options.ApiBaseUrl}/me/onlineMeetings/{Uri.EscapeDataString(meetingId)}"),
            cancellationToken
        );

        // Double-cancel is a no-op upstream — treat 404/410 as success so booking lifecycle
        // never blows up because the meeting was already torn down.
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) return;
        await EnsureSuccessAsync(response, "onlineMeetings.delete", cancellationToken);
    }

    internal static object BuildOnlineMeetingBody(BookingEvent input)
    {
        // Microsoft Graph expects ISO-8601 with the offset (e.g. "2026-01-05T15:00:00Z") on
        // the onlineMeetings endpoint — unlike events.create, there is no separate timeZone
        // field. Round-tripping in UTC keeps the payload deterministic across hosts.
        return new
        {
            startDateTime = input.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            endDateTime = input.EndTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            subject = input.Title
        };
    }

    private static async Task<(string Id, string JoinUrl)> ParseOnlineMeetingAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
                   ?? throw new InvalidOperationException("Microsoft Graph onlineMeetings response was empty.");

        var id = root["id"]?.GetValue<string>()
                 ?? throw new InvalidOperationException("Microsoft Graph onlineMeetings response missing 'id'.");
        // Graph returns both joinUrl (deprecated alias) and joinWebUrl on most tenants — prefer
        // joinWebUrl when present (matches cal.com's office365video adapter), fall back to joinUrl.
        var joinUrl = root["joinWebUrl"]?.GetValue<string>()
                      ?? root["joinUrl"]?.GetValue<string>()
                      ?? throw new InvalidOperationException("Microsoft Graph onlineMeetings response missing 'joinUrl'/'joinWebUrl'.");
        return (id, joinUrl);
    }

    // ─── Internals ──────────────────────────────────────────────────────────

    internal static object BuildEventBody(BookingEvent input)
    {
        var attendees = input.Attendees
            .Select(attendee => new
                {
                    emailAddress = new { address = attendee.Email, name = attendee.Name ?? attendee.Email },
                    type = "required"
                }
            )
            .ToArray();

        return new
        {
            subject = input.Title,
            body = new
            {
                contentType = "HTML",
                content = input.Description ?? string.Empty
            },
            start = new { dateTime = input.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            end = new { dateTime = input.EndTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            originalStartTimeZone = input.TimeZone,
            originalEndTimeZone = input.TimeZone,
            location = input.Location is null ? null : new { displayName = input.Location },
            organizer = new
            {
                emailAddress = new { address = input.OrganizerEmail, name = input.OrganizerName ?? input.OrganizerEmail }
            },
            attendees,
            iCalUId = input.CalUid,
            // Suppress Graph's own notification email — we send our own confirmation flow.
            isReminderOn = false
        };
    }

    private static DateTimeOffset? ReadGraphDateTime(JsonNode? node)
    {
        if (node is null) return null;
        var dateTime = node["dateTime"]?.GetValue<string>();
        if (string.IsNullOrEmpty(dateTime)) return null;
        var timeZone = node["timeZone"]?.GetValue<string>();

        // Graph returns local-style ISO-8601 ("2026-01-02T09:00:00.0000000") with a
        // separate timeZone field (no trailing 'Z'). Because the string has no offset,
        // DateTimeOffset.TryParse would interpret it as the host's local time — instead
        // honour the timeZone field: we always request "UTC" so any other value is
        // unexpected and treated as UTC defensively.
        if (DateTime.TryParse(
                dateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt
            ))
        {
            _ = timeZone; // documented above; we always send UTC
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        }

        return null;
    }

    private async Task<string> EnsureUserPrincipalNameAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(CurrentBlob.UserPrincipalName)) return CurrentBlob.UserPrincipalName;

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{options.ApiBaseUrl}/me?$select=mail,userPrincipalName"),
            cancellationToken
        );
        await EnsureSuccessAsync(response, "me", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        var upn = root?["mail"]?.GetValue<string>() ?? root?["userPrincipalName"]?.GetValue<string>();
        if (string.IsNullOrEmpty(upn))
        {
            throw new InvalidOperationException("Microsoft Graph /me response missing both mail and userPrincipalName.");
        }

        CurrentBlob = CurrentBlob with { UserPrincipalName = upn };
        await persistRefreshedBlobAsync(CurrentBlob.ToJson(), cancellationToken);
        return upn;
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        Func<HttpRequestMessage> buildRequest,
        CancellationToken cancellationToken
    )
    {
        var first = buildRequest();
        Office365CalendarInstaller.ApplyBearer(first, CurrentBlob.AccessToken);
        var response = await httpClient.SendAsync(first, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        await RefreshAccessTokenAsync(cancellationToken);

        var retry = buildRequest();
        Office365CalendarInstaller.ApplyBearer(retry, CurrentBlob.AccessToken);
        return await httpClient.SendAsync(retry, cancellationToken);
    }

    private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["refresh_token"] = CurrentBlob.RefreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = string.Join(' ', options.Scopes)
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenUrl);
        request.Content = content;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Microsoft token refresh failed with status {(int)response.StatusCode}: {body}"
            );
        }

        var token = JsonSerializer.Deserialize<Office365TokenResponse>(body, Office365TokenResponse.JsonOptions)
                    ?? throw new InvalidOperationException("Microsoft token refresh response was empty.");
        if (string.IsNullOrEmpty(token.AccessToken)) throw new InvalidOperationException("Microsoft token refresh response missing access_token.");

        CurrentBlob = new Office365CredentialBlob(
            token.AccessToken,
            // Microsoft rotates the refresh token on every refresh — keep the new one when
            // provided, otherwise fall back to the existing one (defensive).
            string.IsNullOrEmpty(token.RefreshToken) ? CurrentBlob.RefreshToken : token.RefreshToken,
            timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 60)),
            token.Scope ?? CurrentBlob.Scope,
            CurrentBlob.UserPrincipalName
        );

        await persistRefreshedBlobAsync(CurrentBlob.ToJson(), cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Microsoft Graph {operation} failed with status {(int)response.StatusCode}: {body}"
        );
    }
}

/// <summary>
///     Builds an <see cref="Office365CalendarService" /> for a stored <see cref="Credential" />,
///     handling decryption of the JSON blob and wiring the refresh-token persistence callback
///     back through <see cref="ICredentialRepository" />.
/// </summary>
public sealed class Office365CalendarServiceFactory(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<Office365CalendarOptions> options,
    CredentialProtector protector,
    ICredentialRepository credentialRepository,
    TimeProvider timeProvider
)
{
    public Office365CalendarService Create(Credential credential)
    {
        if (credential.AppSlug != Office365CalendarSlug.Slug)
        {
            throw new ArgumentException(
                $"Credential is for app '{credential.AppSlug.Value}', not '{Office365CalendarSlug.Value}'.",
                nameof(credential)
            );
        }

        var blob = Office365CredentialBlob.FromJson(protector.Unprotect(credential.EncryptedKey));
        var client = httpClientFactory.CreateClient(Office365CalendarSlug.HttpClientName);

        return new Office365CalendarService(
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

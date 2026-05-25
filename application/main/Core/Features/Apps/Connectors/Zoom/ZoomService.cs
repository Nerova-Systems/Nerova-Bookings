using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.Extensions.Options;

namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>
///     Per-credential Zoom API client. Creates, updates, and cancels scheduled meetings via
///     <c>POST/PATCH/DELETE /users/me/meetings</c>. Transparently refreshes the OAuth access
///     token on a 401 response and persists the rotated blob back via the supplied callback
///     so subsequent calls reuse the new token without a second refresh round-trip.
///     <para>
///         Construct one instance per credential via <see cref="ZoomServiceFactory" /> — the
///         service is intentionally stateful (it caches the current token blob) and must not
///         be registered as a long-lived singleton.
///     </para>
/// </summary>
public sealed class ZoomService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ZoomOptions _options;
    private readonly Func<string, CancellationToken, Task> _persistRefreshedBlobAsync;
    private readonly TimeProvider _timeProvider;
    private ZoomCredentialBlob _blob;

    public ZoomService(
        HttpClient httpClient,
        ZoomOptions options,
        ZoomCredentialBlob initialBlob,
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

    /// <summary>Snapshot of the in-memory credential — exposed for tests.</summary>
    public ZoomCredentialBlob CurrentBlob => _blob;

    public async Task<ConferenceLink> CreateMeetingAsync(BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildMeetingBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl}/users/me/meetings")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        await EnsureSuccessAsync(response, "users/me/meetings", cancellationToken);
        return await ParseLinkAsync(response, cancellationToken);
    }

    public async Task<ConferenceLink> UpdateMeetingAsync(string meetingId, BookingEvent input, CancellationToken cancellationToken)
    {
        var body = BuildMeetingBody(input);

        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(new HttpMethod("PATCH"), $"{_options.ApiBaseUrl}/meetings/{Uri.EscapeDataString(meetingId)}")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            },
            cancellationToken
        );

        // Zoom returns 204 No Content on a successful PATCH; refetch to surface the (possibly
        // updated) join_url and password back to the caller.
        await EnsureSuccessAsync(response, "meetings.update", cancellationToken);

        using var getResponse = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{_options.ApiBaseUrl}/meetings/{Uri.EscapeDataString(meetingId)}"),
            cancellationToken
        );
        await EnsureSuccessAsync(getResponse, "meetings.get", cancellationToken);
        return await ParseLinkAsync(getResponse, cancellationToken);
    }

    public async Task CancelMeetingAsync(string meetingId, CancellationToken cancellationToken)
    {
        using var response = await SendWithRefreshAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, $"{_options.ApiBaseUrl}/meetings/{Uri.EscapeDataString(meetingId)}"),
            cancellationToken
        );

        // 404 means the meeting is already gone upstream — treat as a successful no-op so a
        // double-cancel from the local lifecycle never blows up the booking flow.
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        await EnsureSuccessAsync(response, "meetings.delete", cancellationToken);
    }

    // ─── Internals ──────────────────────────────────────────────────────────

    internal static object BuildMeetingBody(BookingEvent input)
    {
        var duration = (int)Math.Max(1, Math.Round((input.EndTime - input.StartTime).TotalMinutes));
        return new
        {
            topic = input.Title,
            type = 2, // 2 = Scheduled meeting (per Zoom API docs)
            start_time = input.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            duration,
            timezone = input.TimeZone,
            agenda = TruncateAgenda(input.Description),
            settings = new
            {
                host_video = true,
                participant_video = true,
                join_before_host = false,
                mute_upon_entry = false,
                waiting_room = false,
                approval_type = 2,
                audio = "both"
            }
        };
    }

    /// <summary>
    ///     Zoom enforces a 2000-character limit on <c>agenda</c>. We trim to 1900 to leave a
    ///     safety buffer for the ellipsis (matches cal.com's heuristic).
    /// </summary>
    private static string? TruncateAgenda(string? description)
    {
        if (string.IsNullOrEmpty(description)) return description;
        const int maxLength = 1900;
        var trimmed = description.TrimEnd();
        return trimmed.Length > maxLength ? $"{trimmed[..maxLength].TrimEnd()}..." : trimmed;
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        Func<HttpRequestMessage> buildRequest,
        CancellationToken cancellationToken
    )
    {
        var first = buildRequest();
        ZoomInstaller.ApplyBearer(first, _blob.AccessToken);
        var response = await _httpClient.SendAsync(first, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        await RefreshAccessTokenAsync(cancellationToken);

        var retry = buildRequest();
        ZoomInstaller.ApplyBearer(retry, _blob.AccessToken);
        return await _httpClient.SendAsync(retry, cancellationToken);
    }

    private async Task RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _blob.RefreshToken
                }
            )
        };
        // Zoom requires HTTP Basic auth (client_id:client_secret) on the token endpoint for
        // both the initial code exchange and refresh.
        ZoomInstaller.ApplyBasicAuth(request, _options.ClientId, _options.ClientSecret);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Zoom token refresh failed with status {(int)response.StatusCode}: {body}"
            );
        }

        var token = JsonSerializer.Deserialize<ZoomTokenResponse>(body, ZoomTokenResponse.JsonOptions)
            ?? throw new InvalidOperationException("Zoom token refresh response was empty.");
        if (string.IsNullOrEmpty(token.AccessToken)) throw new InvalidOperationException("Zoom token refresh response missing access_token.");

        _blob = new ZoomCredentialBlob(
            token.AccessToken,
            // Zoom does rotate the refresh token on every refresh — when present, replace it;
            // otherwise keep the existing token so a malformed response doesn't strand us.
            string.IsNullOrEmpty(token.RefreshToken) ? _blob.RefreshToken : token.RefreshToken,
            _timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 60)),
            token.Scope ?? _blob.Scope
        );

        await _persistRefreshedBlobAsync(_blob.ToJson(), cancellationToken);
    }

    private static async Task<ConferenceLink> ParseLinkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Zoom meeting response was empty.");

        // Zoom returns `id` as a number; serialize to string so the BookingReference can carry
        // it without coupling its column to a numeric type.
        var idNode = root["id"] ?? throw new InvalidOperationException("Zoom meeting response missing 'id'.");
        var externalId = idNode.GetValueKind() switch
        {
            JsonValueKind.Number => idNode.GetValue<long>().ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => idNode.GetValue<string>(),
            _ => throw new InvalidOperationException($"Zoom meeting 'id' had unexpected JSON kind {idNode.GetValueKind()}.")
        };

        var joinUrl = root["join_url"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Zoom meeting response missing 'join_url'.");
        var password = root["password"]?.GetValue<string>();
        return new ConferenceLink(externalId, joinUrl, string.IsNullOrEmpty(password) ? null : password);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Zoom {operation} failed with status {(int)response.StatusCode}: {body}"
        );
    }
}

/// <summary>
///     Builds a <see cref="ZoomService" /> for a stored <see cref="Credential" />, handling
///     decryption of the JSON blob and wiring the refresh-token persistence callback back
///     through <see cref="ICredentialRepository" />.
/// </summary>
public sealed class ZoomServiceFactory(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<ZoomOptions> options,
    CredentialProtector protector,
    ICredentialRepository credentialRepository,
    TimeProvider timeProvider
)
{
    public ZoomService Create(Credential credential)
    {
        if (credential.AppSlug != ZoomSlug.Slug)
        {
            throw new ArgumentException(
                $"Credential is for app '{credential.AppSlug.Value}', not '{ZoomSlug.Value}'.",
                nameof(credential)
            );
        }

        var blob = ZoomCredentialBlob.FromJson(protector.Unprotect(credential.EncryptedKey));
        var client = httpClientFactory.CreateClient(ZoomSlug.HttpClientName);

        return new ZoomService(
            client,
            options.CurrentValue,
            blob,
            (newJson, ct) =>
            {
                var encrypted = protector.Protect(newJson);
                credential.UpdateKey(encrypted);
                credentialRepository.Update(credential);
                _ = ct;
                return Task.CompletedTask;
            },
            timeProvider
        );
    }
}

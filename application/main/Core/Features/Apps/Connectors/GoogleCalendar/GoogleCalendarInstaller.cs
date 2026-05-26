using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.Extensions.Options;
using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     Implements the OAuth install lifecycle for Google Calendar. <see cref="BeginInstallAsync" />
///     constructs the authorize URL with the configured scopes; <see cref="CompleteInstallAsync" />
///     exchanges the authorization code for an access + refresh token pair and encrypts the JSON
///     blob with <see cref="CredentialProtector" /> before returning it to the platform.
///     <see cref="UninstallAsync" /> calls Google's revoke endpoint best-effort.
/// </summary>
public sealed class GoogleCalendarInstaller(
    IOptionsMonitor<GoogleCalendarOptions> options,
    IHttpClientFactory httpClientFactory,
    CredentialProtector protector,
    TimeProvider timeProvider,
    ILogger<GoogleCalendarInstaller> logger
) : IAppInstaller
{
    public AppSlug Slug => GoogleCalendarSlug.Slug;

    public Task<AppInstallStartResult> BeginInstallAsync(AppInstallContext context, CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured)
        {
            // Spec: in dev empty client creds should surface as a "not configured" failure. The
            // platform handler does not yet map this to HTTP 412 — once it does, the wrapper can
            // catch this exception and translate it. Deferred wiring is intentional and tracked.
            throw new GoogleCalendarNotConfiguredException();
        }

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = opts.ClientId;
        query["redirect_uri"] = context.RedirectUri;
        query["response_type"] = "code";
        query["scope"] = string.Join(' ', opts.Scopes);
        query["access_type"] = "offline";
        query["include_granted_scopes"] = "true";
        query["prompt"] = "consent";
        query["state"] = context.State;
        query["login_hint"] = context.UserEmail;

        var authorizeUrl = $"{opts.AuthorizeUrl}?{query}";
        return Task.FromResult(new AppInstallStartResult(authorizeUrl, context.State));
    }

    public async Task<AppInstallCallbackResult> CompleteInstallAsync(
        AppInstallCallbackContext context,
        CancellationToken cancellationToken
    )
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured) throw new GoogleCalendarNotConfiguredException();

        var client = httpClientFactory.CreateClient(GoogleCalendarSlug.HttpClientName);
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = context.Code,
                ["client_id"] = opts.ClientId,
                ["client_secret"] = opts.ClientSecret,
                ["redirect_uri"] = context.RedirectUri,
                ["grant_type"] = "authorization_code"
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, opts.TokenUrl);
        request.Content = content;

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google token exchange failed with status {(int)response.StatusCode}: {body}"
            );
        }

        var token = JsonSerializer.Deserialize<GoogleTokenResponse>(body, GoogleTokenResponse.JsonOptions)
                    ?? throw new InvalidOperationException("Google token response was empty.");
        if (string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.RefreshToken))
        {
            throw new InvalidOperationException("Google token response missing access_token or refresh_token.");
        }

        var blob = new GoogleCredentialBlob(
            token.AccessToken,
            token.RefreshToken,
            timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 60)),
            token.Scope ?? string.Join(' ', opts.Scopes)
        );
        var encrypted = protector.Protect(blob.ToJson());
        return new AppInstallCallbackResult(encrypted);
    }

    public async Task UninstallAsync(TenantId tenantId, UserId userId, string encryptedKey, CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured) return;

        GoogleCredentialBlob blob;
        try
        {
            blob = GoogleCredentialBlob.FromJson(protector.Unprotect(encryptedKey));
        }
        catch (Exception exception)
        {
            // If the blob is corrupted there is nothing to revoke at Google; log and proceed so
            // the platform can still drop the local credential row.
            logger.LogWarning(exception, "Failed to decrypt Google credential during uninstall; skipping revocation.");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(GoogleCalendarSlug.HttpClientName);
            var revokeContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = blob.RefreshToken
                }
            );
            using var request = new HttpRequestMessage(HttpMethod.Post, opts.RevokeUrl);
            request.Content = revokeContent;
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Google revoke endpoint returned {Status} for user {UserId}; local credential will still be removed.",
                    (int)response.StatusCode, userId.Value
                );
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Google revoke call failed for user {UserId}; local credential will still be removed.", userId.Value);
        }
    }

    /// <summary>Sets a Bearer header from the given access token. Shared with <see cref="GoogleCalendarService" />.</summary>
    internal static void ApplyBearer(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}

public sealed class GoogleCalendarNotConfiguredException()
    : InvalidOperationException("Google Calendar OAuth client credentials are not configured.");

internal sealed record GoogleTokenResponse(
    [property: JsonPropertyName("access_token")]
    string AccessToken,
    [property: JsonPropertyName("refresh_token")]
    string? RefreshToken,
    [property: JsonPropertyName("expires_in")]
    int ExpiresIn,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("token_type")]
    string? TokenType
)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

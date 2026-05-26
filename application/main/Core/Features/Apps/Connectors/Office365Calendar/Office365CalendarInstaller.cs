using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.Extensions.Options;
using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.Office365Calendar;

/// <summary>
///     Implements the OAuth install lifecycle for Microsoft Office 365 (Outlook) Calendar
///     against the Microsoft identity platform v2.0 endpoints.
///     <see cref="BeginInstallAsync" /> constructs the authorize URL with the configured
///     scopes; <see cref="CompleteInstallAsync" /> exchanges the authorization code for an
///     access + refresh token pair and encrypts the JSON blob with
///     <see cref="CredentialProtector" /> before returning it to the platform.
///     <see cref="UninstallAsync" /> is a no-op — Microsoft does not expose a clean revoke
///     endpoint, so the platform simply drops the local credential row.
/// </summary>
public sealed class Office365CalendarInstaller(
    IOptionsMonitor<Office365CalendarOptions> options,
    IHttpClientFactory httpClientFactory,
    CredentialProtector protector,
    TimeProvider timeProvider,
    ILogger<Office365CalendarInstaller> logger
) : IAppInstaller
{
    public AppSlug Slug => Office365CalendarSlug.Slug;

    public Task<AppInstallStartResult> BeginInstallAsync(AppInstallContext context, CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured)
        {
            // Same deferral as the Google connector: the platform handler does not yet map
            // "not configured" to HTTP 412 — once it does, the wrapper can catch this and
            // translate. Intentional and tracked.
            throw new Office365CalendarNotConfiguredException();
        }

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = opts.ClientId;
        query["redirect_uri"] = context.RedirectUri;
        query["response_type"] = "code";
        query["response_mode"] = "query";
        query["scope"] = string.Join(' ', opts.Scopes);
        query["state"] = context.State;
        // Microsoft uses login_hint with the user's UPN/email to skip the account picker.
        query["login_hint"] = context.UserEmail;
        // Force consent so the user always sees the requested scopes — mirrors Google's
        // prompt=consent behaviour and guarantees a refresh token on first install.
        query["prompt"] = "consent";

        var authorizeUrl = $"{opts.AuthorizeUrl}?{query}";
        return Task.FromResult(new AppInstallStartResult(authorizeUrl, context.State));
    }

    public async Task<AppInstallCallbackResult> CompleteInstallAsync(
        AppInstallCallbackContext context,
        CancellationToken cancellationToken
    )
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured) throw new Office365CalendarNotConfiguredException();

        var client = httpClientFactory.CreateClient(Office365CalendarSlug.HttpClientName);
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = context.Code,
                ["client_id"] = opts.ClientId,
                ["client_secret"] = opts.ClientSecret,
                ["redirect_uri"] = context.RedirectUri,
                ["grant_type"] = "authorization_code",
                // Microsoft requires scope on the token exchange to mirror the authorize
                // call; omitting it returns a partial scope set.
                ["scope"] = string.Join(' ', opts.Scopes)
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, opts.TokenUrl);
        request.Content = content;

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Microsoft token exchange failed with status {(int)response.StatusCode}: {body}"
            );
        }

        var token = JsonSerializer.Deserialize<Office365TokenResponse>(body, Office365TokenResponse.JsonOptions)
                    ?? throw new InvalidOperationException("Microsoft token response was empty.");
        if (string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.RefreshToken))
        {
            throw new InvalidOperationException("Microsoft token response missing access_token or refresh_token.");
        }

        var blob = new Office365CredentialBlob(
            token.AccessToken,
            token.RefreshToken,
            timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 60)),
            token.Scope ?? string.Join(' ', opts.Scopes)
            // We don't have the user principal name from the token response itself; the
            // service layer falls back to the /me endpoint on first event create if needed.
        );
        var encrypted = protector.Protect(blob.ToJson());
        return new AppInstallCallbackResult(encrypted);
    }

    public Task UninstallAsync(TenantId tenantId, UserId userId, string encryptedKey, CancellationToken cancellationToken)
    {
        // Microsoft Graph does not expose a clean per-token revocation endpoint (only a
        // per-app admin-consent revoke). Drop the local credential row — the user can
        // revoke the consent at https://account.microsoft.com/privacy/app-access if needed.
        _ = tenantId;
        _ = userId;
        _ = encryptedKey;
        logger.LogDebug("Office 365 uninstall: no remote revoke available; local credential row will be removed.");
        return Task.CompletedTask;
    }

    /// <summary>Sets a Bearer header from the given access token. Shared with <see cref="Office365CalendarService" />.</summary>
    internal static void ApplyBearer(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}

public sealed class Office365CalendarNotConfiguredException()
    : InvalidOperationException("Microsoft Office 365 Calendar OAuth client credentials are not configured.");

internal sealed record Office365TokenResponse(
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

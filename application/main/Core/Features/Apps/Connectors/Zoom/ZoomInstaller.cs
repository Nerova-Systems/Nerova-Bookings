using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.Extensions.Options;
using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.Zoom;

/// <summary>
///     Implements the OAuth install lifecycle for Zoom. <see cref="BeginInstallAsync" />
///     constructs the authorize URL; <see cref="CompleteInstallAsync" /> exchanges the
///     authorization code for an access + refresh token pair (HTTP Basic auth with the client
///     credentials, per Zoom OAuth spec) and encrypts the JSON blob via
///     <see cref="CredentialProtector" />. <see cref="UninstallAsync" /> POSTs to Zoom's
///     <c>/oauth/revoke</c> endpoint best-effort.
/// </summary>
public sealed class ZoomInstaller(
    IOptionsMonitor<ZoomOptions> options,
    IHttpClientFactory httpClientFactory,
    CredentialProtector protector,
    TimeProvider timeProvider,
    ILogger<ZoomInstaller> logger
) : IAppInstaller
{
    public AppSlug Slug => ZoomSlug.Slug;

    public IReadOnlyList<AppPermission> Permissions => ZoomPermissions.All;

    public Task<AppInstallStartResult> BeginInstallAsync(AppInstallContext context, CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured) throw new ZoomNotConfiguredException();

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = opts.ClientId;
        query["redirect_uri"] = context.RedirectUri;
        query["state"] = context.State;

        var authorizeUrl = $"{opts.AuthorizeUrl}?{query}";
        return Task.FromResult(new AppInstallStartResult(authorizeUrl, context.State));
    }

    public async Task<AppInstallCallbackResult> CompleteInstallAsync(
        AppInstallCallbackContext context,
        CancellationToken cancellationToken
    )
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured) throw new ZoomNotConfiguredException();

        var client = httpClientFactory.CreateClient(ZoomSlug.HttpClientName);
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = context.Code,
                ["redirect_uri"] = context.RedirectUri
            }
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, opts.TokenUrl);
        request.Content = content;
        ApplyBasicAuth(request, opts.ClientId, opts.ClientSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Zoom token exchange failed with status {(int)response.StatusCode}: {body}"
            );
        }

        var token = JsonSerializer.Deserialize<ZoomTokenResponse>(body, ZoomTokenResponse.JsonOptions)
                    ?? throw new InvalidOperationException("Zoom token response was empty.");
        if (string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.RefreshToken))
        {
            throw new InvalidOperationException("Zoom token response missing access_token or refresh_token.");
        }

        var blob = new ZoomCredentialBlob(
            token.AccessToken,
            token.RefreshToken,
            timeProvider.GetUtcNow().AddSeconds(Math.Max(token.ExpiresIn - 60, 60)),
            token.Scope ?? string.Empty
        );
        var encrypted = protector.Protect(blob.ToJson());
        return new AppInstallCallbackResult(encrypted);
    }

    public async Task UninstallAsync(TenantId tenantId, UserId userId, string encryptedKey, CancellationToken cancellationToken)
    {
        var opts = options.CurrentValue;
        if (!opts.IsConfigured) return;

        ZoomCredentialBlob blob;
        try
        {
            blob = ZoomCredentialBlob.FromJson(protector.Unprotect(encryptedKey));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to decrypt Zoom credential during uninstall; skipping revocation.");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(ZoomSlug.HttpClientName);
            var revokeContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = blob.AccessToken
                }
            );
            using var request = new HttpRequestMessage(HttpMethod.Post, opts.RevokeUrl);
            request.Content = revokeContent;
            // Revoke also uses Basic auth with the client credentials.
            ApplyBasicAuth(request, opts.ClientId, opts.ClientSecret);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Zoom revoke endpoint returned {Status} for user {UserId}; local credential will still be removed.",
                    (int)response.StatusCode, userId.Value
                );
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Zoom revoke call failed for user {UserId}; local credential will still be removed.", userId.Value);
        }
    }

    /// <summary>Sets a Bearer header from the given access token. Shared with <see cref="ZoomService" />.</summary>
    internal static void ApplyBearer(HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>Sets the HTTP Basic auth header used by Zoom for OAuth token and revoke calls.</summary>
    internal static void ApplyBasicAuth(HttpRequestMessage request, string clientId, string clientSecret)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }
}

public sealed class ZoomNotConfiguredException()
    : InvalidOperationException("Zoom OAuth client credentials are not configured.");

internal sealed record ZoomTokenResponse(
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

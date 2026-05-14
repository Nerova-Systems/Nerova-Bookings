using System.Net.Http.Json;
using System.Text.Json;
using Account.Features.ExternalAuthentication.Domain;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;

namespace Account.Integrations.OAuth.Facebook;

internal sealed record FacebookOAuthConfiguration(
    string ClientId,
    string ClientSecret,
    string? GraphApiVersion = null,
    string? LoginConfigurationId = null
);

public sealed class FacebookOAuthProvider(HttpClient httpClient, IConfiguration configuration, ILogger<FacebookOAuthProvider> logger) : IOAuthProvider
{
    private const string DefaultGraphApiVersion = "v23.0";

    private readonly FacebookOAuthConfiguration _configuration = GetConfiguration(configuration);

    private string GraphApiVersion => string.IsNullOrWhiteSpace(_configuration.GraphApiVersion) ? DefaultGraphApiVersion : _configuration.GraphApiVersion;

    public ExternalProviderType ProviderType => ExternalProviderType.Facebook;

    public string BuildAuthorizationUrl(string stateToken, string codeChallenge, string nonce, string redirectUri)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _configuration.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["state"] = stateToken,
            ["auth_type"] = "rerequest",
            ["config_id"] = _configuration.LoginConfigurationId!,
            ["override_default_response_type"] = "true"
        };

        var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"https://www.facebook.com/{GraphApiVersion}/dialog/oauth?{queryString}";
    }

    public async Task<OAuthTokenResponse?> ExchangeCodeForTokensAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = _configuration.ClientId,
                ["client_secret"] = _configuration.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["code"] = code
            };

            var queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            var response = await httpClient.GetAsync($"https://graph.facebook.com/{GraphApiVersion}/oauth/access_token?{queryString}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogTokenExchangeError(response, cancellationToken);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<FacebookTokenResponse>(cancellationToken);
            if (tokenResponse is null) return null;

            return new OAuthTokenResponse(tokenResponse.AccessToken, null, tokenResponse.ExpiresIn);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<OAuthUserProfile?> GetUserProfileAsync(OAuthTokenResponse tokenResponse, CancellationToken cancellationToken)
    {
        try
        {
            var fields = Uri.EscapeDataString("id,email,first_name,last_name,picture.type(large),locale");
            var accessToken = Uri.EscapeDataString(tokenResponse.AccessToken);
            var response = await httpClient.GetAsync($"https://graph.facebook.com/{GraphApiVersion}/me?fields={fields}&access_token={accessToken}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                await LogProfileError(response, cancellationToken);
                return null;
            }

            var profile = await response.Content.ReadFromJsonAsync<FacebookProfileResponse>(cancellationToken);
            if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || string.IsNullOrWhiteSpace(profile.Email)) return null;

            return new OAuthUserProfile(
                profile.Id,
                profile.Email,
                true,
                profile.FirstName,
                profile.LastName,
                profile.Picture?.Data?.Url,
                profile.Locale,
                null
            );
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static FacebookOAuthConfiguration GetConfiguration(IConfiguration configuration)
    {
        var facebookConfiguration = configuration.GetSection("OAuth:Facebook").Get<FacebookOAuthConfiguration>()
                                    ?? throw new InvalidOperationException("OAuth:Facebook configuration is missing.");

        if (string.IsNullOrWhiteSpace(facebookConfiguration.LoginConfigurationId))
        {
            throw new InvalidOperationException("OAuth:Facebook:LoginConfigurationId is required for Facebook Login for Business.");
        }

        return facebookConfiguration;
    }

    private async Task LogTokenExchangeError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var error = await ReadFacebookError(response, cancellationToken);
        logger.LogWarning("Facebook token exchange failed with status '{StatusCode}': {Error}", response.StatusCode, error);
    }

    private async Task LogProfileError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var error = await ReadFacebookError(response, cancellationToken);
        logger.LogWarning("Facebook profile request failed with status '{StatusCode}': {Error}", response.StatusCode, error);
    }

    private static async Task<string> ReadFacebookError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        const int maxBodyLength = 500;
        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(errorBody);
            if (!document.RootElement.TryGetProperty("error", out var errorElement)) return errorBody;

            var message = errorElement.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
            var type = errorElement.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            return string.IsNullOrWhiteSpace(type) ? message ?? errorBody : $"{type}: {message}";
        }
        catch (JsonException)
        {
            return errorBody.Length > maxBodyLength ? errorBody[..maxBodyLength] : errorBody;
        }
    }

    private sealed record FacebookTokenResponse(
        [property: JsonPropertyName("access_token")]
        string AccessToken,
        [property: JsonPropertyName("token_type")]
        string TokenType,
        [property: JsonPropertyName("expires_in")]
        int ExpiresIn
    );

    private sealed record FacebookProfileResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("first_name")]
        string? FirstName,
        [property: JsonPropertyName("last_name")]
        string? LastName,
        [property: JsonPropertyName("picture")]
        FacebookPictureResponse? Picture,
        [property: JsonPropertyName("locale")] string? Locale
    );

    [UsedImplicitly]
    private sealed record FacebookPictureResponse(
        [property: JsonPropertyName("data")] FacebookPictureDataResponse? Data
    );

    [UsedImplicitly]
    private sealed record FacebookPictureDataResponse(
        [property: JsonPropertyName("url")] string? Url
    );
}

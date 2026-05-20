using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Database;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SharedKernel.Domain;

namespace Main.Features.Connectors.Domain;

public sealed record CoreConnectorAuthorizationUrlResponse(string Url);

public sealed record CoreConnectorTokenSet(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string? Scope,
    string TokenType
);

public sealed record CoreConnectorOAuthAccount(
    string ExternalAccountId,
    string AccountEmail,
    string DisplayName,
    CoreConnectorCalendar[] Calendars
);

public sealed record CoreConnectorOAuthCallbackResult(CoreConnectorTokenSet TokenSet, CoreConnectorOAuthAccount Account);

public sealed record CoreConnectorOAuthState(
    TenantId TenantId,
    UserId OwnerUserId,
    string Integration,
    string ReturnTo,
    string Nonce,
    DateTimeOffset ExpiresAt
);

public sealed class CoreConnectorOAuthException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IConnectorTokenStore
{
    Task SaveAsync(TenantId tenantId, string id, string credentialId, CoreConnectorTokenSet tokenSet, CancellationToken cancellationToken);

    Task<CoreConnectorTokenSet?> GetAsync(TenantId tenantId, string id, CancellationToken cancellationToken);

    Task RemoveForCredentialAsync(TenantId tenantId, string credentialId, CancellationToken cancellationToken);
}

public sealed class ProtectedConnectorTokenStore(
    IConnectorTokenSecretRepository connectorTokenSecretRepository,
    IDataProtectionProvider dataProtectionProvider,
    MainDbContext mainDbContext
) : IConnectorTokenStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Main.CoreConnectors.TokenSecrets");

    public async Task SaveAsync(TenantId tenantId, string id, string credentialId, CoreConnectorTokenSet tokenSet, CancellationToken cancellationToken)
    {
        var protectedPayload = _protector.Protect(JsonSerializer.Serialize(tokenSet, JsonSerializerOptions));
        var existingSecret = await connectorTokenSecretRepository.GetForTenantAsync(tenantId, id, cancellationToken);
        if (existingSecret is null)
        {
            await connectorTokenSecretRepository.AddAsync(ConnectorTokenSecret.Create(tenantId, id, credentialId, protectedPayload), cancellationToken);
        }
        else
        {
            existingSecret.UpdateProtectedPayload(protectedPayload);
            connectorTokenSecretRepository.Update(existingSecret);
        }

        await mainDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CoreConnectorTokenSet?> GetAsync(TenantId tenantId, string id, CancellationToken cancellationToken)
    {
        var secret = await connectorTokenSecretRepository.GetForTenantAsync(tenantId, id, cancellationToken);
        if (secret is null) return null;

        var payload = _protector.Unprotect(secret.ProtectedPayload);
        return JsonSerializer.Deserialize<CoreConnectorTokenSet>(payload, JsonSerializerOptions);
    }

    public async Task RemoveForCredentialAsync(TenantId tenantId, string credentialId, CancellationToken cancellationToken)
    {
        var secret = await connectorTokenSecretRepository.GetForCredentialAsync(tenantId, credentialId, cancellationToken);
        if (secret is null) return;

        connectorTokenSecretRepository.Remove(secret);
        await mainDbContext.SaveChangesAsync(cancellationToken);
    }
}

public interface ICoreConnectorOAuthProvider
{
    bool Supports(string integration);

    bool IsConfigured();

    string BuildAuthorizationUrl(string redirectUri, string state);

    Task<CoreConnectorOAuthCallbackResult> CompleteCallbackAsync(string code, string redirectUri, CancellationToken cancellationToken);

    Task<CoreConnectorTokenSet> RefreshAsync(CoreConnectorTokenSet tokenSet, CancellationToken cancellationToken);
}

public sealed class CoreConnectorOAuthProviderRegistry(IEnumerable<ICoreConnectorOAuthProvider> providers)
{
    public ICoreConnectorOAuthProvider? GetProvider(string integration)
    {
        return providers.FirstOrDefault(provider => provider.Supports(integration));
    }
}

public sealed class ProtectedCoreConnectorAccessTokenProvider(
    IConnectorTokenStore connectorTokenStore,
    CoreConnectorOAuthProviderRegistry providerRegistry,
    IConfiguration configuration
) : ICoreConnectorAccessTokenProvider
{
    private const string ProtectedTokenReferencePrefix = "protected-connector-token:";

    public async Task<string?> GetAccessTokenAsync(ConnectorCredential credential, CancellationToken cancellationToken)
    {
        if (!credential.SecretReference.StartsWith(ProtectedTokenReferencePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return configuration[$"Connectors:Core:AccessTokens:{credential.Id}"];
        }

        var tokenSecretId = credential.SecretReference[ProtectedTokenReferencePrefix.Length..].Trim();
        var tokenSet = await connectorTokenStore.GetAsync(credential.TenantId, tokenSecretId, cancellationToken);
        if (tokenSet is null) return null;
        if (tokenSet.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5)) return tokenSet.AccessToken;
        if (string.IsNullOrWhiteSpace(tokenSet.RefreshToken)) return tokenSet.AccessToken;

        var provider = providerRegistry.GetProvider(credential.Integration);
        if (provider is null) throw new HttpRequestException($"OAuth provider '{credential.Integration}' is not supported.");

        var refreshedTokenSet = await provider.RefreshAsync(tokenSet, cancellationToken);
        await connectorTokenStore.SaveAsync(credential.TenantId, tokenSecretId, credential.Id, refreshedTokenSet, cancellationToken);
        return refreshedTokenSet.AccessToken;
    }
}

public abstract class CoreConnectorOAuthProviderBase(IConfiguration configuration, IHostEnvironment hostEnvironment, IHttpClientFactory httpClientFactory)
    : ICoreConnectorOAuthProvider
{
    protected abstract string Integration { get; }

    protected abstract string AuthorizationEndpoint { get; }

    protected abstract string TokenEndpoint { get; }

    protected virtual string? ProfileEndpoint => null;

    protected virtual string? CalendarListEndpoint => null;

    protected abstract string[] Scopes { get; }

    protected string ClientId => ConfigValue("ClientId", $"test-{Integration}-client-id");

    protected string ClientSecret => ConfigValue("ClientSecret", $"test-{Integration}-client-secret");

    protected string ProviderOptionsName => Integration switch
    {
        CoreConnectorConstants.GoogleCalendar => "GoogleCalendar",
        CoreConnectorConstants.Office365Calendar => "Office365Calendar",
        CoreConnectorConstants.ZoomVideo => "Zoom",
        _ => Integration
    };

    public bool Supports(string integration)
    {
        return integration.Equals(Integration, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsConfigured()
    {
        return IsConfiguredValue(ClientId) && IsConfiguredValue(ClientSecret);
    }

    public virtual string BuildAuthorizationUrl(string redirectUri, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = string.Join(' ', Scopes),
            ["state"] = state
        };
        AddAuthorizationParameters(query);
        return $"{AuthorizationEndpoint}?{BuildQueryString(query)}";
    }

    public async Task<CoreConnectorOAuthCallbackResult> CompleteCallbackAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        if (code.Equals("mock-provider-error", StringComparison.OrdinalIgnoreCase))
        {
            throw new CoreConnectorOAuthException("provider_error", "Provider rejected the OAuth callback code.");
        }

        if (TryCompleteMockCallback(code, out var mockResult)) return mockResult;

        var tokenSet = await ExchangeCodeAsync(code, redirectUri, cancellationToken);
        var account = await DiscoverAccountAsync(tokenSet.AccessToken, cancellationToken);
        return new CoreConnectorOAuthCallbackResult(tokenSet, account);
    }

    public async Task<CoreConnectorTokenSet> RefreshAsync(CoreConnectorTokenSet tokenSet, CancellationToken cancellationToken)
    {
        if (TryRefreshMockToken(tokenSet, out var refreshedMockToken)) return refreshedMockToken;
        if (string.IsNullOrWhiteSpace(tokenSet.RefreshToken))
        {
            throw new CoreConnectorOAuthException("provider_error", "Connector credential does not contain a refresh token.");
        }

        return await RefreshProviderTokenAsync(tokenSet, cancellationToken);
    }

    protected abstract bool TryCompleteMockCallback(string code, [NotNullWhen(true)] out CoreConnectorOAuthCallbackResult? result);

    protected abstract bool TryRefreshMockToken(CoreConnectorTokenSet tokenSet, [NotNullWhen(true)] out CoreConnectorTokenSet? refreshedToken);

    protected virtual void AddAuthorizationParameters(Dictionary<string, string?> query)
    {
    }

    protected virtual HttpRequestMessage CreateTokenRequest(Dictionary<string, string> formValues)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
        request.Content = new FormUrlEncodedContent(formValues);
        return request;
    }

    protected async Task<CoreConnectorOAuthAccount> DiscoverAccountAsync(string accessToken, CancellationToken cancellationToken)
    {
        JsonElement? profile = string.IsNullOrWhiteSpace(ProfileEndpoint)
            ? null
            : await GetJsonAsync<JsonElement>(ProfileEndpoint, accessToken, cancellationToken);
        var calendars = string.IsNullOrWhiteSpace(CalendarListEndpoint)
            ? []
            : await DiscoverCalendarsAsync(accessToken, cancellationToken);

        var email = ExtractString(profile, "email", "userPrincipalName", "mail") ?? "connected@example.test";
        var id = ExtractString(profile, "id", "sub", "account_id") ?? email;
        var displayName = ExtractString(profile, "name", "displayName", "first_name") ?? email;
        return new CoreConnectorOAuthAccount(id, email, displayName, calendars);
    }

    protected async Task<CoreConnectorCalendar[]> DiscoverCalendarsAsync(string accessToken, CancellationToken cancellationToken)
    {
        var calendars = await GetJsonAsync<JsonElement>(CalendarListEndpoint!, accessToken, cancellationToken);
        if (calendars.TryGetProperty("items", out var googleItems))
        {
            return googleItems.EnumerateArray()
                .Select(item => new CoreConnectorCalendar(
                        ExtractString(item, "id") ?? "primary",
                        ExtractString(item, "summary") ?? ExtractString(item, "name") ?? "Calendar",
                        item.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True
                    )
                )
                .ToArray();
        }

        if (calendars.TryGetProperty("value", out var graphItems))
        {
            return graphItems.EnumerateArray()
                .Select(item => new CoreConnectorCalendar(
                        ExtractString(item, "id") ?? "calendar",
                        ExtractString(item, "name") ?? "Calendar",
                        ExtractString(item, "isDefaultCalendar")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
                    )
                )
                .ToArray();
        }

        return [];
    }

    protected static string BuildQueryString(Dictionary<string, string?> query)
    {
        return string.Join(
            '&',
            query.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}")
        );
    }

    private async Task<CoreConnectorTokenSet> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        var request = CreateTokenRequest(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }
        );
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CoreConnectorOAuthException("provider_error", "Provider token exchange failed.");
        }

        var token = await response.Content.ReadFromJsonAsync<ProviderTokenResponse>(JsonSerializerOptions.Default, cancellationToken)
                    ?? throw new CoreConnectorOAuthException("provider_error", "Provider token response was empty.");
        return MapTokenResponse(token);
    }

    private async Task<CoreConnectorTokenSet> RefreshProviderTokenAsync(CoreConnectorTokenSet tokenSet, CancellationToken cancellationToken)
    {
        var request = CreateTokenRequest(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = tokenSet.RefreshToken!,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret
            }
        );
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CoreConnectorOAuthException("provider_error", "Provider token refresh failed.");
        }

        var token = await response.Content.ReadFromJsonAsync<ProviderTokenResponse>(JsonSerializerOptions.Default, cancellationToken)
                    ?? throw new CoreConnectorOAuthException("provider_error", "Provider refresh response was empty.");
        return MapTokenResponse(token, tokenSet.RefreshToken);
    }

    private static CoreConnectorTokenSet MapTokenResponse(ProviderTokenResponse token, string? fallbackRefreshToken = null)
    {
        return new CoreConnectorTokenSet(
            token.AccessToken ?? throw new CoreConnectorOAuthException("provider_error", "Provider token response did not include an access token."),
            string.IsNullOrWhiteSpace(token.RefreshToken) ? fallbackRefreshToken : token.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn ?? 3600)),
            token.Scope,
            string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType
        );
    }

    private async Task<T> GetJsonAsync<T>(string endpoint, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CoreConnectorOAuthException("provider_error", "Provider account discovery failed.");
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions.Default, cancellationToken)
               ?? throw new CoreConnectorOAuthException("provider_error", "Provider account discovery response was empty.");
    }

    private string ConfigValue(string key, string developmentDefault)
    {
        var configuredValue = configuration[$"Connectors:Core:OAuth:{ProviderOptionsName}:{key}"];
        if (!string.IsNullOrWhiteSpace(configuredValue)) return configuredValue;
        return hostEnvironment.IsDevelopment() ? developmentDefault : string.Empty;
    }

    private static bool IsConfiguredValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && !value.Equals("not-configured", StringComparison.OrdinalIgnoreCase);
    }

    protected static string? ExtractString(JsonElement? element, params string[] names)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        foreach (var name in names)
        {
            if (!element.Value.TryGetProperty(name, out var property)) continue;
            if (property.ValueKind == JsonValueKind.String) return property.GetString();
            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False) return property.GetBoolean().ToString().ToLowerInvariant();
            if (property.ValueKind == JsonValueKind.Number) return property.ToString();
        }

        return null;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ProviderTokenResponse(
        [property: JsonPropertyName("access_token")]
        string? AccessToken,
        [property: JsonPropertyName("refresh_token")]
        string? RefreshToken,
        [property: JsonPropertyName("expires_in")]
        int? ExpiresIn,
        [property: JsonPropertyName("token_type")]
        string? TokenType,
        string? Scope
    );
}

public sealed class GoogleCalendarOAuthProvider(IConfiguration configuration, IHostEnvironment hostEnvironment, IHttpClientFactory httpClientFactory)
    : CoreConnectorOAuthProviderBase(configuration, hostEnvironment, httpClientFactory)
{
    protected override string Integration => CoreConnectorConstants.GoogleCalendar;

    protected override string AuthorizationEndpoint => "https://accounts.google.com/o/oauth2/v2/auth";

    protected override string TokenEndpoint => "https://oauth2.googleapis.com/token";

    protected override string ProfileEndpoint => "https://openidconnect.googleapis.com/v1/userinfo";

    protected override string CalendarListEndpoint => "https://www.googleapis.com/calendar/v3/users/me/calendarList";

    protected override string[] Scopes => ["https://www.googleapis.com/auth/calendar", "https://www.googleapis.com/auth/userinfo.profile"];

    protected override void AddAuthorizationParameters(Dictionary<string, string?> query)
    {
        query["access_type"] = "offline";
        query["prompt"] = "consent";
    }

    protected override bool TryCompleteMockCallback(string code, [NotNullWhen(true)] out CoreConnectorOAuthCallbackResult? result)
    {
        result = code.Equals("mock-google-success", StringComparison.OrdinalIgnoreCase)
            ? new CoreConnectorOAuthCallbackResult(
                new CoreConnectorTokenSet("google-access-token", "mock-google-refresh-token", DateTimeOffset.UtcNow.AddHours(1), string.Join(' ', Scopes), "Bearer"),
                new CoreConnectorOAuthAccount(
                    "google-account-1",
                    "owner.google@example.test",
                    "Owner Google",
                    [new CoreConnectorCalendar("primary", "Primary calendar", true), new CoreConnectorCalendar("focus", "Focus calendar", false)]
                )
            )
            : null;
        return result is not null;
    }

    protected override bool TryRefreshMockToken(CoreConnectorTokenSet tokenSet, [NotNullWhen(true)] out CoreConnectorTokenSet? refreshedToken)
    {
        refreshedToken = tokenSet.RefreshToken?.Equals("mock-google-refresh-token", StringComparison.OrdinalIgnoreCase) == true
            ? tokenSet with { AccessToken = "refreshed-google-access-token", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) }
            : null;
        return refreshedToken is not null;
    }
}

public sealed class Office365CalendarOAuthProvider(IConfiguration configuration, IHostEnvironment hostEnvironment, IHttpClientFactory httpClientFactory)
    : CoreConnectorOAuthProviderBase(configuration, hostEnvironment, httpClientFactory)
{
    protected override string Integration => CoreConnectorConstants.Office365Calendar;

    protected override string AuthorizationEndpoint => "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";

    protected override string TokenEndpoint => "https://login.microsoftonline.com/common/oauth2/v2.0/token";

    protected override string ProfileEndpoint => "https://graph.microsoft.com/v1.0/me";

    protected override string CalendarListEndpoint => "https://graph.microsoft.com/v1.0/me/calendars";

    protected override string[] Scopes => ["offline_access", "Calendars.Read", "Calendars.ReadWrite", "User.Read"];

    protected override bool TryCompleteMockCallback(string code, [NotNullWhen(true)] out CoreConnectorOAuthCallbackResult? result)
    {
        result = code.Equals("mock-office365-success", StringComparison.OrdinalIgnoreCase)
            ? new CoreConnectorOAuthCallbackResult(
                new CoreConnectorTokenSet("office365-access-token", "mock-office365-refresh-token", DateTimeOffset.UtcNow.AddHours(1), string.Join(' ', Scopes), "Bearer"),
                new CoreConnectorOAuthAccount(
                    "office365-account-1",
                    "owner.office@example.test",
                    "Owner Office 365",
                    [new CoreConnectorCalendar("calendar", "Calendar", true), new CoreConnectorCalendar("team", "Team calendar", false)]
                )
            )
            : null;
        return result is not null;
    }

    protected override bool TryRefreshMockToken(CoreConnectorTokenSet tokenSet, [NotNullWhen(true)] out CoreConnectorTokenSet? refreshedToken)
    {
        refreshedToken = tokenSet.RefreshToken?.Equals("mock-office365-refresh-token", StringComparison.OrdinalIgnoreCase) == true
            ? tokenSet with { AccessToken = "refreshed-office365-access-token", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) }
            : null;
        return refreshedToken is not null;
    }
}

public sealed class ZoomOAuthProvider(IConfiguration configuration, IHostEnvironment hostEnvironment, IHttpClientFactory httpClientFactory)
    : CoreConnectorOAuthProviderBase(configuration, hostEnvironment, httpClientFactory)
{
    protected override string Integration => CoreConnectorConstants.ZoomVideo;

    protected override string AuthorizationEndpoint => "https://zoom.us/oauth/authorize";

    protected override string TokenEndpoint => "https://zoom.us/oauth/token";

    protected override string ProfileEndpoint => "https://api.zoom.us/v2/users/me";

    protected override string[] Scopes => [];

    protected override HttpRequestMessage CreateTokenRequest(Dictionary<string, string> formValues)
    {
        formValues.Remove("client_id");
        formValues.Remove("client_secret");
        var request = base.CreateTokenRequest(formValues);
        var encodedCredential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredential);
        return request;
    }

    protected override bool TryCompleteMockCallback(string code, [NotNullWhen(true)] out CoreConnectorOAuthCallbackResult? result)
    {
        result = code.Equals("mock-zoom-success", StringComparison.OrdinalIgnoreCase)
            ? new CoreConnectorOAuthCallbackResult(
                new CoreConnectorTokenSet("zoom-access-token", "mock-zoom-refresh-token", DateTimeOffset.UtcNow.AddHours(1), null, "Bearer"),
                new CoreConnectorOAuthAccount("zoom-account-1", "owner.zoom@example.test", "Owner Zoom", [])
            )
            : null;
        return result is not null;
    }

    protected override bool TryRefreshMockToken(CoreConnectorTokenSet tokenSet, [NotNullWhen(true)] out CoreConnectorTokenSet? refreshedToken)
    {
        refreshedToken = tokenSet.RefreshToken?.Equals("mock-zoom-refresh-token", StringComparison.OrdinalIgnoreCase) == true
            ? tokenSet with { AccessToken = "refreshed-zoom-access-token", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) }
            : null;
        return refreshedToken is not null;
    }
}

public static class CoreConnectorCredentialIds
{
    public const string ProtectedTokenReferencePrefix = "protected-connector-token:";

    public static string CredentialId(TenantId tenantId, UserId ownerUserId, string integration, string externalAccountId)
    {
        return $"core-{ShortHash($"{tenantId.Value}:{ownerUserId.Value}:{integration}:{externalAccountId}")}";
    }

    public static string TokenSecretId(TenantId tenantId, UserId ownerUserId, string integration, string externalAccountId)
    {
        return $"secret-{ShortHash($"{tenantId.Value}:{ownerUserId.Value}:{integration}:{externalAccountId}")}";
    }

    private static string ShortHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..32].ToLowerInvariant();
    }
}

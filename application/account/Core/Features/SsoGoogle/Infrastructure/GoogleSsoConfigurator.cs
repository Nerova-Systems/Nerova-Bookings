using System.Text.Json;
using Account.Features.Sso.Domain;
using Account.Features.Tenants.Domain;
using SharedKernel.Domain;

namespace Account.Features.SsoGoogle.Infrastructure;

public sealed record ResolvedGoogleSsoConfig(
    TenantId OrgId,
    string ClientId,
    string ClientSecret,
    string HostedDomain,
    string[] AllowedDomains
);

internal sealed record GoogleProviderConfig(
    string ClientId,
    string ClientSecret,
    string HostedDomain
);

/// <summary>
///     Resolves the per-organization Google Workspace SSO configuration at request time by decrypting
///     the stored provider config blob. Used by both management commands and the OIDC initiate/callback flow.
/// </summary>
public sealed class GoogleSsoConfigurator(
    IOrgSsoConfigRepository ssoConfigRepository,
    GoogleSsoSecretProtector secretProtector,
    ITenantRepository tenantRepository
)
{
    public const string GoogleDiscoveryUrl = "https://accounts.google.com/.well-known/openid-configuration";
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public static string GetAuthorizationEndpoint()
    {
        return AuthorizationEndpoint;
    }

    public static string GetTokenEndpoint()
    {
        return TokenEndpoint;
    }

    /// <summary>
    ///     Resolves the Google SSO config for the org identified by <paramref name="orgSlug" />.
    ///     Returns <see langword="null" /> if the org or config does not exist, or SSO is not enabled.
    /// </summary>
    public async Task<ResolvedGoogleSsoConfig?> ResolveForOrgSlugAsync(string orgSlug, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetBySlugAsync(orgSlug, ct);
        if (tenant is null) return null;

        return await ResolveForOrgIdAsync(tenant.Id, ct);
    }

    /// <summary>
    ///     Resolves the Google SSO config for the org identified by <paramref name="orgId" />.
    ///     Returns <see langword="null" /> if no config exists or SSO is not enabled.
    /// </summary>
    public async Task<ResolvedGoogleSsoConfig?> ResolveForOrgIdAsync(TenantId orgId, CancellationToken ct)
    {
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Google, ct);
        if (config is null || !config.IsEnabled) return null;

        return Decrypt(config);
    }

    /// <summary>
    ///     Decrypts and returns the Google provider config regardless of enabled state.
    ///     Used by management commands that need full config access.
    /// </summary>
    public ResolvedGoogleSsoConfig? DecryptConfig(OrgSsoConfig config)
    {
        if (config.Provider != SsoProvider.Google) return null;
        return Decrypt(config);
    }

    public string EncryptConfig(string clientId, string clientSecret, string hostedDomain)
    {
        var provider = new GoogleProviderConfig(clientId, clientSecret, hostedDomain);
        var json = JsonSerializer.Serialize(provider);
        return secretProtector.Protect(json);
    }

    private ResolvedGoogleSsoConfig Decrypt(OrgSsoConfig config)
    {
        var json = secretProtector.Unprotect(config.EncryptedProviderConfig);
        var provider = JsonSerializer.Deserialize<GoogleProviderConfig>(json)
                       ?? throw new InvalidOperationException("Failed to deserialize Google provider config.");

        return new ResolvedGoogleSsoConfig(
            config.TenantId,
            provider.ClientId,
            provider.ClientSecret,
            provider.HostedDomain,
            config.GetAllowedDomains()
        );
    }
}

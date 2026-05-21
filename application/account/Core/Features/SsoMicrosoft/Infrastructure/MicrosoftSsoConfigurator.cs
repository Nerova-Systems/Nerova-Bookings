using System.Text.Json;
using Account.Features.Sso.Domain;
using Account.Features.Tenants.Domain;
using SharedKernel.Domain;

namespace Account.Features.SsoMicrosoft.Infrastructure;

public sealed record ResolvedMicrosoftSsoConfig(
    TenantId OrgId,
    string AzureTenantId,
    string ClientId,
    string ClientSecret,
    string[] AllowedDomains
);

internal sealed record MicrosoftProviderConfig(
    string AzureTenantId,
    string ClientId,
    string ClientSecret
);

/// <summary>
///     Resolves the per-organization Microsoft SSO configuration at request time by decrypting the
///     stored provider config blob. Used by both management commands and the OIDC initiate/callback flow.
/// </summary>
public sealed class MicrosoftSsoConfigurator(
    IOrgSsoConfigRepository ssoConfigRepository,
    MicrosoftSsoSecretProtector secretProtector,
    ITenantRepository tenantRepository
)
{
    private const string MicrosoftAuthorityBase = "https://login.microsoftonline.com";

    public static string GetAuthorityUrl(string azureTenantId)
    {
        return $"{MicrosoftAuthorityBase}/{azureTenantId}/v2.0";
    }

    public static string GetAuthorizationEndpoint(string azureTenantId)
    {
        return $"{MicrosoftAuthorityBase}/{azureTenantId}/oauth2/v2.0/authorize";
    }

    public static string GetTokenEndpoint(string azureTenantId)
    {
        return $"{MicrosoftAuthorityBase}/{azureTenantId}/oauth2/v2.0/token";
    }

    public static string GetDiscoveryUrl(string azureTenantId)
    {
        return $"{MicrosoftAuthorityBase}/{azureTenantId}/v2.0/.well-known/openid-configuration";
    }

    /// <summary>
    ///     Resolves the Microsoft SSO config for the org identified by <paramref name="orgSlug" />.
    ///     Returns <see langword="null" /> if the org or config does not exist, or SSO is not enabled.
    /// </summary>
    public async Task<ResolvedMicrosoftSsoConfig?> ResolveForOrgSlugAsync(string orgSlug, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetBySlugAsync(orgSlug, ct);
        if (tenant is null) return null;

        return await ResolveForOrgIdAsync(tenant.Id, ct);
    }

    /// <summary>
    ///     Resolves the Microsoft SSO config for the org identified by <paramref name="orgId" />.
    ///     Returns <see langword="null" /> if no config exists or SSO is not enabled.
    /// </summary>
    public async Task<ResolvedMicrosoftSsoConfig?> ResolveForOrgIdAsync(TenantId orgId, CancellationToken ct)
    {
        var config = await ssoConfigRepository.GetByOrgAndProviderAsync(orgId, SsoProvider.Microsoft, ct);
        if (config is null || !config.IsEnabled) return null;

        return Decrypt(config);
    }

    /// <summary>
    ///     Decrypts and returns the Microsoft provider config regardless of enabled state.
    ///     Used by management commands that need full config access.
    /// </summary>
    public ResolvedMicrosoftSsoConfig? DecryptConfig(OrgSsoConfig config)
    {
        if (config.Provider != SsoProvider.Microsoft) return null;
        return Decrypt(config);
    }

    public string EncryptConfig(string azureTenantId, string clientId, string clientSecret)
    {
        var provider = new MicrosoftProviderConfig(azureTenantId, clientId, clientSecret);
        var json = JsonSerializer.Serialize(provider);
        return secretProtector.Protect(json);
    }

    private ResolvedMicrosoftSsoConfig Decrypt(OrgSsoConfig config)
    {
        var json = secretProtector.Unprotect(config.EncryptedProviderConfig);
        var provider = JsonSerializer.Deserialize<MicrosoftProviderConfig>(json)
                       ?? throw new InvalidOperationException("Failed to deserialize Microsoft provider config.");

        return new ResolvedMicrosoftSsoConfig(
            config.TenantId,
            provider.AzureTenantId,
            provider.ClientId,
            provider.ClientSecret,
            config.GetAllowedDomains()
        );
    }
}

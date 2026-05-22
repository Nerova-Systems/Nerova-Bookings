using System.Text.Json;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Sso.Domain;

[PublicAPI]
[IdPrefix("sso")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, OrgSsoConfigId>))]
public sealed record OrgSsoConfigId(string Value) : StronglyTypedUlid<OrgSsoConfigId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Per-organization SSO configuration for a specific identity provider.
///     Invariant: one config per (org, provider) — enforced by unique index on (tenant_id, provider).
/// </summary>
public sealed class OrgSsoConfig : AggregateRoot<OrgSsoConfigId>, ITenantScopedEntity
{
    private OrgSsoConfig(OrgSsoConfigId id) : base(id)
    {
    }

    public SsoProvider Provider { get; private set; }

    /// <summary>
    ///     Full provider config JSON encrypted via <c>MicrosoftSsoSecretProtector</c>. Never exposed
    ///     in plain text outside the infrastructure layer.
    /// </summary>
    public string EncryptedProviderConfig { get; private set; } = null!;

    /// <summary>
    ///     Email domains allowed to sign in via this SSO config.
    ///     Stored as a JSON array (e.g. <c>["acme.com","acme.org"]</c>).
    /// </summary>
    public string AllowedDomainsJson { get; private set; } = null!;

    /// <summary>
    ///     When <see langword="false" />, the SSO flow is rejected even if a config exists.
    /// </summary>
    public bool IsEnabled { get; private set; }

    public TenantId TenantId { get; private init; } = null!;

    // ─── Helpers ──────────────────────────────────────────────────────────────

    public string[] GetAllowedDomains()
    {
        return JsonSerializer.Deserialize<string[]>(AllowedDomainsJson) ?? [];
    }

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new SSO configuration for the given organization. The config starts enabled.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="tenant" /> is not a <see cref="TenantKind.Organization" />.
    /// </exception>
    public static OrgSsoConfig Create(
        Tenant tenant,
        SsoProvider provider,
        string encryptedProviderConfig,
        string[] allowedDomains)
    {
        if (tenant.Kind != TenantKind.Organization)
        {
            throw new InvalidOperationException("SSO configuration can only be created for organization tenants.");
        }

        return new OrgSsoConfig(OrgSsoConfigId.NewId())
        {
            TenantId = tenant.Id,
            Provider = provider,
            EncryptedProviderConfig = encryptedProviderConfig,
            AllowedDomainsJson = JsonSerializer.Serialize(allowedDomains),
            IsEnabled = true
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    public void Update(string encryptedProviderConfig, string[] allowedDomains)
    {
        EncryptedProviderConfig = encryptedProviderConfig;
        AllowedDomainsJson = JsonSerializer.Serialize(allowedDomains);
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }
}

public interface IOrgSsoConfigRepository : ICrudRepository<OrgSsoConfig, OrgSsoConfigId>
{
    /// <summary>
    ///     Returns the SSO configuration for the given organization and provider, or
    ///     <see langword="null" /> if no configuration has been set.
    /// </summary>
    Task<OrgSsoConfig?> GetByOrgAndProviderAsync(TenantId orgId, SsoProvider provider, CancellationToken cancellationToken);
}

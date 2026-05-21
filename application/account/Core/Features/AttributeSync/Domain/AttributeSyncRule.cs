using Account.Features.Attributes.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.AttributeSync.Domain;

/// <summary>
///     Strongly-typed identifier for an <see cref="AttributeSyncRule" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>asr</c>.
/// </summary>
[PublicAPI]
[IdPrefix("asr")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, AttributeSyncRuleId>))]
public sealed record AttributeSyncRuleId(string Value) : StronglyTypedUlid<AttributeSyncRuleId>(Value)
{
    public override string ToString() => Value;
}

/// <summary>
///     Defines a rule that maps a specific IdP claim to an org attribute assignment.
///     On every SSO login the <c>AttributeSyncService</c> evaluates all enabled rules for the
///     org and applies idempotent add/remove operations on the member's attribute assignments.
///     <para>
///         Ports cal.com <c>packages/features/ee/attributes/lib/attributeSyncUtils.ts</c>.
///     </para>
/// </summary>
public sealed class AttributeSyncRule : AggregateRoot<AttributeSyncRuleId>, ITenantScopedEntity
{
    private AttributeSyncRule(AttributeSyncRuleId id) : base(id) { }

    /// <summary>The org tenant this rule belongs to.</summary>
    public TenantId TenantId { get; private set; } = null!;

    /// <summary>The attribute whose assignments this rule manages.</summary>
    public AttributeId AttributeId { get; private set; } = null!;

    /// <summary>
    ///     The SAML/OIDC claim key to read from the IdP token (e.g., <c>"department"</c>
    ///     or <c>"groups[]"</c>). A <c>[]</c> suffix signals that the claim is array-valued
    ///     and is stripped before querying the claims dictionary.
    /// </summary>
    public string ClaimPath { get; private set; } = null!;

    /// <summary>Determines how the extracted claim value is mapped to an assignment.</summary>
    public ClaimMappingMode Mode { get; private set; }

    /// <summary>
    ///     When <see langword="true" /> and <see cref="Mode" /> is
    ///     <see cref="ClaimMappingMode.Lookup" /> or <see cref="ClaimMappingMode.Group" />,
    ///     a new option is auto-created on the attribute when no slug match is found.
    ///     When <see langword="false" />, unmatched values are silently skipped.
    /// </summary>
    public bool AutoCreateOptions { get; private set; }

    /// <summary>When <see langword="false" /> the rule is skipped during sync.</summary>
    public bool IsEnabled { get; private set; }

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>Creates a new enabled sync rule for the given org.</summary>
    public static AttributeSyncRule Create(
        TenantId orgTenantId,
        AttributeId attributeId,
        string claimPath,
        ClaimMappingMode mode,
        bool autoCreateOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimPath);

        return new AttributeSyncRule(AttributeSyncRuleId.NewId())
        {
            TenantId = orgTenantId,
            AttributeId = attributeId,
            ClaimPath = claimPath.Trim(),
            Mode = mode,
            AutoCreateOptions = autoCreateOptions,
            IsEnabled = true
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    /// <summary>Updates mutable fields of this rule.</summary>
    public void Update(AttributeId attributeId, string claimPath, ClaimMappingMode mode, bool autoCreateOptions, bool isEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimPath);
        AttributeId = attributeId;
        ClaimPath = claimPath.Trim();
        Mode = mode;
        AutoCreateOptions = autoCreateOptions;
        IsEnabled = isEnabled;
    }
}

/// <summary>Persistence contract for <see cref="AttributeSyncRule" /> aggregates.</summary>
public interface IAttributeSyncRuleRepository : ICrudRepository<AttributeSyncRule, AttributeSyncRuleId>
{
    /// <summary>Returns all sync rules for the given org, ordered newest-first.</summary>
    Task<IReadOnlyList<AttributeSyncRule>> GetByOrgUnfilteredAsync(
        TenantId orgTenantId,
        CancellationToken cancellationToken);

    /// <summary>Returns all enabled sync rules for the given org.</summary>
    Task<IReadOnlyList<AttributeSyncRule>> GetEnabledByOrgUnfilteredAsync(
        TenantId orgTenantId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Loads a single rule by ID, bypassing the tenant query filter.
    ///     Returns <see langword="null" /> if not found.
    /// </summary>
    Task<AttributeSyncRule?> GetByIdUnfilteredAsync(
        AttributeSyncRuleId id,
        CancellationToken cancellationToken);
}

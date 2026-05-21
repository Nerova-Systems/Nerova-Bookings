using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.DelegationCredentials.Domain;

/// <summary>
///     Strongly-typed identifier for a <see cref="DelegationCredential" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>dcrd</c>.
/// </summary>
[PublicAPI]
[IdPrefix("dcrd")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, DelegationCredentialId>))]
public sealed record DelegationCredentialId(string Value) : StronglyTypedUlid<DelegationCredentialId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Lifecycle status of a <see cref="DelegationCredential" />.
/// </summary>
[PublicAPI]
public enum DelegationCredentialStatus
{
    /// <summary>The credential is active and eligible for resolution.</summary>
    Active,

    /// <summary>The credential has been disabled by an org admin and will not be resolved.</summary>
    Inactive
}

/// <summary>
///     The outcome of the most recent connectivity test for a <see cref="DelegationCredential" />.
/// </summary>
[PublicAPI]
public enum CredentialTestStatus
{
    /// <summary>The last test succeeded.</summary>
    Success,

    /// <summary>The last test failed.</summary>
    Failed
}

/// <summary>
///     Per-org delegation credential used to impersonate org members when calling Google Calendar
///     or Microsoft Graph (busy-time queries, conferencing).
///     <para>
///         Invariant: at most one active credential per <c>(org, platform)</c> pair, enforced by
///         the unique index <c>uix_delegation_credentials_tenant_id_platform</c>.
///     </para>
/// </summary>
public sealed class DelegationCredential : AggregateRoot<DelegationCredentialId>, ITenantScopedEntity
{
    private DelegationCredential(DelegationCredentialId id) : base(id)
    {
    }

    public WorkspacePlatform Platform { get; private set; }

    /// <summary>
    ///     The email domain this credential covers (e.g. <c>acme.com</c>).
    ///     Only org members whose email address ends with <c>@{Domain}</c> are covered.
    /// </summary>
    public string Domain { get; private set; } = null!;

    /// <summary>
    ///     The key blob (service-account JSON or OAuth refresh token) encrypted via
    ///     <c>DelegationCredentialEncryption</c>. Never exposed in plain text outside the
    ///     infrastructure layer.
    /// </summary>
    public string EncryptedKeyBlob { get; private set; } = null!;

    public DelegationCredentialStatus Status { get; private set; }

    public DateTimeOffset? LastTestedAt { get; private set; }

    public CredentialTestStatus? LastTestStatus { get; private set; }

    public string? LastTestError { get; private set; }

    public UserId CreatedByUserId { get; private set; } = null!;

    public TenantId TenantId { get; private set; } = null!;

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new delegation credential for the given organization tenant.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="tenant" /> is not a <see cref="TenantKind.Organization" />.
    /// </exception>
    public static DelegationCredential Create(
        Tenant tenant,
        WorkspacePlatform platform,
        string domain,
        string encryptedKeyBlob,
        UserId createdByUserId)
    {
        if (tenant.Kind != TenantKind.Organization)
        {
            throw new InvalidOperationException("Delegation credentials can only be created for organization tenants.");
        }

        return new DelegationCredential(DelegationCredentialId.NewId())
        {
            TenantId = tenant.Id,
            Platform = platform,
            Domain = domain.ToLowerInvariant(),
            EncryptedKeyBlob = encryptedKeyBlob,
            Status = DelegationCredentialStatus.Active,
            CreatedByUserId = createdByUserId
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    /// <summary>
    ///     Replaces the encrypted key blob and optionally updates the covered domain.
    ///     Use when an org admin rotates their Google service account key or Microsoft client secret.
    /// </summary>
    public void RotateKey(string encryptedKeyBlob, string domain)
    {
        EncryptedKeyBlob = encryptedKeyBlob;
        Domain = domain.ToLowerInvariant();
    }

    public void Enable()
    {
        Status = DelegationCredentialStatus.Active;
    }

    public void Disable()
    {
        Status = DelegationCredentialStatus.Inactive;
    }

    /// <summary>
    ///     Records the outcome of the most recent connectivity test.
    /// </summary>
    public void MarkTestResult(bool success, string? error, DateTimeOffset testedAt)
    {
        LastTestedAt = testedAt;
        LastTestStatus = success ? CredentialTestStatus.Success : CredentialTestStatus.Failed;
        LastTestError = success ? null : error;
    }
}

public interface IDelegationCredentialRepository : ICrudRepository<DelegationCredential, DelegationCredentialId>
{
    /// <summary>
    ///     Returns the credential for the given org and platform, or <see langword="null" /> if none
    ///     has been configured.
    /// </summary>
    Task<DelegationCredential?> GetByOrgAndPlatformAsync(
        TenantId orgId,
        WorkspacePlatform platform,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all credentials configured for the given org (up to two — one per platform).
    /// </summary>
    Task<DelegationCredential[]> GetAllByOrgIdAsync(TenantId orgId, CancellationToken cancellationToken);
}

using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Smtp.Domain;

/// <summary>
///     Strongly-typed identifier for a <see cref="OrgSmtpConfig" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>smtp</c>.
/// </summary>
[PublicAPI]
[IdPrefix("smtp")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, OrgSmtpConfigId>))]
public sealed record OrgSmtpConfigId(string Value) : StronglyTypedUlid<OrgSmtpConfigId>(Value)
{
    public override string ToString() => Value;
}

/// <summary>
///     Per-organization SMTP configuration override.
///     When present and enabled, the <c>TenantAwareEmailClient</c> uses these credentials to send
///     outbound emails for the organization instead of the platform default email provider.
///     <para>
///         Invariant: only one config per organization (enforced by the unique index on
///         <c>org_smtp_configs.tenant_id</c>).
///     </para>
/// </summary>
public sealed class OrgSmtpConfig : AggregateRoot<OrgSmtpConfigId>, ITenantScopedEntity
{
    private OrgSmtpConfig(OrgSmtpConfigId id) : base(id) { }

    public TenantId TenantId { get; private set; } = null!;

    public string Host { get; private set; } = null!;

    public int Port { get; private set; }

    public bool UseSsl { get; private set; }

    public string Username { get; private set; } = null!;

    /// <summary>
    ///     SMTP password encrypted via <c>SmtpCredentialProtector</c>. Never exposed in plain text
    ///     outside the infrastructure layer.
    /// </summary>
    public string EncryptedPassword { get; private set; } = null!;

    public string FromEmail { get; private set; } = null!;

    public string? FromName { get; private set; }

    public string? ReplyToEmail { get; private set; }

    /// <summary>
    ///     When <see langword="false" />, the platform default email provider is used even if a config exists.
    /// </summary>
    public bool IsEnabled { get; private set; }

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new SMTP configuration for the given organization tenant.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="tenant" /> is not a <see cref="TenantKind.Organization" />.
    /// </exception>
    public static OrgSmtpConfig Create(
        Tenant tenant,
        string host,
        int port,
        bool useSsl,
        string username,
        string encryptedPassword,
        string fromEmail,
        string? fromName,
        string? replyToEmail)
    {
        if (tenant.Kind != TenantKind.Organization)
            throw new InvalidOperationException("SMTP configuration can only be created for organization tenants.");

        return new OrgSmtpConfig(OrgSmtpConfigId.NewId())
        {
            TenantId = tenant.Id,
            Host = host,
            Port = port,
            UseSsl = useSsl,
            Username = username,
            EncryptedPassword = encryptedPassword,
            FromEmail = fromEmail,
            FromName = fromName,
            ReplyToEmail = replyToEmail,
            IsEnabled = true
        };
    }

    // ─── Mutations ────────────────────────────────────────────────────────────

    public void Update(
        string host,
        int port,
        bool useSsl,
        string username,
        string encryptedPassword,
        string fromEmail,
        string? fromName,
        string? replyToEmail)
    {
        Host = host;
        Port = port;
        UseSsl = useSsl;
        Username = username;
        EncryptedPassword = encryptedPassword;
        FromEmail = fromEmail;
        FromName = fromName;
        ReplyToEmail = replyToEmail;
    }

    public void Enable() => IsEnabled = true;

    public void Disable() => IsEnabled = false;
}

public interface IOrgSmtpConfigRepository : ICrudRepository<OrgSmtpConfig, OrgSmtpConfigId>
{
    /// <summary>
    ///     Returns the SMTP configuration for the given organization, or <see langword="null" />
    ///     if no configuration has been set.
    /// </summary>
    Task<OrgSmtpConfig?> GetByOrgIdAsync(TenantId orgId, CancellationToken cancellationToken);
}

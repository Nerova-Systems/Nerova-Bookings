using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.AuditLog.Domain;

/// <summary>
///     Strongly-typed identifier for an <see cref="AuditLogEntry" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>audit</c>.
/// </summary>
[PublicAPI]
[IdPrefix("audit")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, AuditLogEntryId>))]
public sealed record AuditLogEntryId(string Value) : StronglyTypedUlid<AuditLogEntryId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Immutable audit log record capturing who did what, to which resource, and when.
///     Implements <see cref="ITenantScopedEntity" /> so EF Core's global query filter automatically
///     scopes all reads to the current tenant — only the owning tenant can query its own log.
///     <para>
///         Mirrors cal.com's audit-log infrastructure
///         (<see href="https://github.com/calcom/cal.com/tree/main/packages/features/audit-logs" />).
///     </para>
///     <para>
///         Entries are append-only. No mutation methods are exposed.
///     </para>
/// </summary>
public sealed class AuditLogEntry : AggregateRoot<AuditLogEntryId>, ITenantScopedEntity
{
    private AuditLogEntry(
        AuditLogEntryId id,
        TenantId tenantId,
        UserId? actorUserId,
        string actorEmail,
        string resource,
        string action,
        string? resourceId,
        string? metadata,
        string? ipAddress,
        string? userAgent)
        : base(id)
    {
        TenantId = tenantId;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail;
        Resource = resource;
        Action = action;
        ResourceId = resourceId;
        Metadata = metadata;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    /// <summary>The user who triggered this event, or <see langword="null" /> for system-initiated actions.</summary>
    public UserId? ActorUserId { get; }

    /// <summary>Snapshot of the actor's email at the time the event was emitted. Immutable.</summary>
    public string ActorEmail { get; }

    /// <summary>The type of resource affected (e.g. <c>Membership</c>, <c>Role</c>). Stored verbatim.</summary>
    public string Resource { get; }

    /// <summary>The action performed (e.g. <c>Invited</c>, <c>Deleted</c>). Stored verbatim.</summary>
    public string Action { get; }

    /// <summary>Opaque cross-aggregate resource identifier (e.g. <c>mbr_01ABCDEF...</c>).</summary>
    public string? ResourceId { get; }

    /// <summary>JSON-serialized dictionary of additional event context.</summary>
    public string? Metadata { get; }

    /// <summary>Client IP address at the time the event was emitted.</summary>
    public string? IpAddress { get; }

    /// <summary>Client user-agent string at the time the event was emitted.</summary>
    public string? UserAgent { get; }

    /// <summary>The tenant whose log this entry belongs to.</summary>
    public TenantId TenantId { get; }

    // ─── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new <see cref="AuditLogEntry" />.
    ///     Called by <c>AuditLogEmitter</c> when an <c>AuditLogEvent</c> is received.
    /// </summary>
    public static AuditLogEntry Create(
        TenantId tenantId,
        UserId? actorUserId,
        string actorEmail,
        string resource,
        string action,
        string? resourceId = null,
        string? metadata = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        return new AuditLogEntry(
            AuditLogEntryId.NewId(),
            tenantId,
            actorUserId,
            actorEmail,
            resource,
            action,
            resourceId,
            metadata,
            ipAddress,
            userAgent
        );
    }
}

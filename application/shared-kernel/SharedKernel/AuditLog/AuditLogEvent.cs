using SharedKernel.Domain;

namespace SharedKernel.AuditLog;

/// <summary>
///     Represents an auditable event emitted by any SCS.
///     The <see cref="IAuditLogEmitter" /> in the Account SCS receives this event and persists it as an
///     immutable <c>AuditLogEntry</c> aggregate.
/// </summary>
/// <param name="TenantId">The tenant that owns this event.</param>
/// <param name="ActorId">The user who triggered the action, or <see langword="null" /> for system-initiated actions.</param>
/// <param name="ActorEmail">Snapshot of the actor's email at the time of the event (immutable record).</param>
/// <param name="Resource">The resource type affected (e.g. <c>Membership</c>, <c>Role</c>). Stored verbatim.</param>
/// <param name="Action">The action performed (e.g. <c>Invited</c>, <c>Deleted</c>). Stored verbatim.</param>
/// <param name="ResourceId">Opaque string identifying the specific resource instance, if applicable.</param>
/// <param name="Metadata">Arbitrary key-value pairs for additional context. Serialized as JSON at persistence time.</param>
/// <param name="IpAddress">Client IP address, if available.</param>
/// <param name="UserAgent">Client user-agent string, if available.</param>
[PublicAPI]
public record AuditLogEvent(
    TenantId TenantId,
    UserId? ActorId,
    string ActorEmail,
    string Resource,
    string Action,
    string? ResourceId = null,
    IReadOnlyDictionary<string, string>? Metadata = null,
    string? IpAddress = null,
    string? UserAgent = null);

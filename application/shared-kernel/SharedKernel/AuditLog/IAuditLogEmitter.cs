namespace SharedKernel.AuditLog;

/// <summary>
///     Contract for persisting audit events from any SCS without coupling to the Account assembly.
///     Register implementations in DI; the Account SCS provides the concrete <c>AuditLogEmitter</c>.
/// </summary>
public interface IAuditLogEmitter
{
    /// <summary>Persists an <see cref="AuditLogEvent" /> as an immutable audit log record.</summary>
    Task EmitAsync(AuditLogEvent evt, CancellationToken cancellationToken = default);
}

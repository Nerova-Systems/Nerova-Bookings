using System.Text.Json;
using Account.Features.AuditLog.Domain;
using SharedKernel.AuditLog;

namespace Account.Features.AuditLog.Infrastructure;

/// <summary>
///     Implements <see cref="IAuditLogEmitter" /> by mapping <see cref="AuditLogEvent" /> DTOs to
///     <see cref="AuditLogEntry" /> aggregates and staging them for persistence.
/// </summary>
public sealed class AuditLogEmitter(IAuditLogRepository repository) : IAuditLogEmitter
{
    public async Task EmitAsync(AuditLogEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var metadata = evt.Metadata is { Count: > 0 }
            ? JsonSerializer.Serialize(evt.Metadata)
            : null;

        var entry = AuditLogEntry.Create(
            evt.TenantId,
            evt.ActorId,
            evt.ActorEmail,
            evt.Resource,
            evt.Action,
            evt.ResourceId,
            metadata,
            evt.IpAddress,
            evt.UserAgent);

        await repository.AddAsync(entry, cancellationToken);
    }
}

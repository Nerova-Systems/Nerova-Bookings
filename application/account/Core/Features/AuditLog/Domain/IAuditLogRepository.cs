using SharedKernel.Domain;

namespace Account.Features.AuditLog.Domain;

/// <summary>
///     Optional filter parameters for <see cref="IAuditLogRepository.GetPagedAsync" />.
///     All properties are additive (AND semantics); <see langword="null" /> means "no filter on this field".
/// </summary>
public sealed record AuditLogFilter(
    UserId? ActorUserId = null,
    string? Resource = null,
    string? Action = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null);

/// <summary>
///     Repository contract for <see cref="AuditLogEntry" /> aggregates.
///     The implementation is append-only — entries are never mutated or deleted via this contract.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>Stages a new <see cref="AuditLogEntry" /> for insertion on the next <c>SaveChangesAsync</c>.</summary>
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns a page of <see cref="AuditLogEntry" /> records matching <paramref name="filter" />,
    ///     ordered by <c>CreatedAt</c> descending (most recent first).
    ///     The <see cref="ITenantScopedEntity" /> query filter is applied automatically.
    /// </summary>
    Task<(AuditLogEntry[] Items, int TotalCount)> GetPagedAsync(
        AuditLogFilter filter,
        int pageOffset,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Returns the <see cref="AuditLogEntry" /> with the specified <paramref name="id" />,
    ///     or <see langword="null" /> if not found (or not accessible in the current tenant).
    /// </summary>
    Task<AuditLogEntry?> GetByIdAsync(AuditLogEntryId id, CancellationToken cancellationToken);
}

using Account.Database;
using Account.Features.AuditLog.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;

namespace Account.Features.AuditLog.Infrastructure;

/// <summary>
///     Append-only repository for <see cref="AuditLogEntry" /> aggregates.
///     All reads are automatically scoped to the current tenant via the
///     <see cref="ITenantScopedEntity" /> query filter.
/// </summary>
public sealed class AuditLogRepository(AccountDbContext context)
    : RepositoryBase<AuditLogEntry, AuditLogEntryId>(context), IAuditLogRepository
{
    // AddAsync and GetByIdAsync are inherited from RepositoryBase.

    public async Task<(AuditLogEntry[] Items, int TotalCount)> GetPagedAsync(
        AuditLogFilter filter,
        int pageOffset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = DbSet.AsNoTracking();

        if (filter.ActorUserId is not null)
        {
            query = query.Where(e => e.ActorUserId == filter.ActorUserId);
        }

        if (filter.Resource is not null)
        {
            query = query.Where(e => e.Resource == filter.Resource);
        }

        if (filter.Action is not null)
        {
            query = query.Where(e => e.Action == filter.Action);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(e => e.CreatedAt <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // SQLite does not support DateTimeOffset in ORDER BY clauses; materialize then sort in memory.
        if (context.Database.ProviderName is "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var all = await query.ToArrayAsync(cancellationToken);
            var sorted = all.OrderByDescending(e => e.CreatedAt)
                .Skip(pageOffset * pageSize)
                .Take(pageSize)
                .ToArray();
            return (sorted, totalCount);
        }

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(pageOffset * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return (items, totalCount);
    }
}

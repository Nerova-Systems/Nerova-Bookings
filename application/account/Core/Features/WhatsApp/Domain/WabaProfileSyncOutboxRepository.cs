using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.WhatsApp.Domain;

public interface IWabaProfileSyncOutboxRepository : ICrudRepository<WabaProfileSyncOutbox, WabaProfileSyncOutboxId>
{
    /// <summary>
    ///     Returns the next pending row whose <see cref="WabaProfileSyncOutbox.NextAttemptAt" /> is
    ///     due (i.e. less than or equal to <paramref name="now" />), oldest first. Used by the
    ///     Phase 7b sync job to pick the next item of work.
    /// </summary>
    Task<WabaProfileSyncOutbox?> GetNextDueAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns up to <paramref name="maxCount" /> rows eligible for processing right now:
    ///     either <see cref="WabaProfileSyncStatus.Pending" /> or
    ///     <see cref="WabaProfileSyncStatus.Failed" /> rows whose retry backoff has elapsed and
    ///     that have not yet exhausted <see cref="WabaProfileSyncOutbox.MaxAttempts" />. Ordered
    ///     by <see cref="WabaProfileSyncOutbox.NextAttemptAt" /> ascending so the oldest backlog
    ///     drains first.
    /// </summary>
    Task<List<WabaProfileSyncOutbox>> GetBatchDueAsync(DateTimeOffset now, int maxCount, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns <see langword="true" /> if the tenant has at least one row that is neither
    ///     <see cref="WabaProfileSyncStatus.Synced" /> nor terminally failed. The drift detector
    ///     uses this for idempotency: if a sync is already in-flight or scheduled to retry, it
    ///     will not enqueue another one.
    /// </summary>
    Task<bool> HasNonTerminalForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public sealed class WabaProfileSyncOutboxRepository(AccountDbContext dbContext)
    : RepositoryBase<WabaProfileSyncOutbox, WabaProfileSyncOutboxId>(dbContext), IWabaProfileSyncOutboxRepository
{
    public Task<WabaProfileSyncOutbox?> GetNextDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        return DbSet
            .Where(x => x.Status == WabaProfileSyncStatus.Pending && x.NextAttemptAt != null && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<WabaProfileSyncOutbox>> GetBatchDueAsync(DateTimeOffset now, int maxCount, CancellationToken cancellationToken)
    {
        return DbSet
            .Where(x =>
                (x.Status == WabaProfileSyncStatus.Pending ||
                 (x.Status == WabaProfileSyncStatus.Failed && x.Attempts < WabaProfileSyncOutbox.MaxAttempts))
                && x.NextAttemptAt != null
                && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasNonTerminalForTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.AnyAsync(
            x => x.TenantId == tenantId &&
                 x.Status != WabaProfileSyncStatus.Synced &&
                 !(x.Status == WabaProfileSyncStatus.Failed && x.Attempts >= WabaProfileSyncOutbox.MaxAttempts),
            cancellationToken);
    }
}

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
}

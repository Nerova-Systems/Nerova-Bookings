using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Schedules.Domain;

public interface IOutOfOfficeRepository : ICrudRepository<OutOfOffice, OutOfOfficeId>
{
    Task<OutOfOffice[]> GetForUserAsync(UserId userId, CancellationToken cancellationToken);

    Task<OutOfOffice[]> GetActiveForUserUnfilteredAsync(TenantId tenantId, UserId userId, DateOnly windowStart, DateOnly windowEnd, CancellationToken cancellationToken);
}

public sealed class OutOfOfficeRepository(MainDbContext mainDbContext)
    : RepositoryBase<OutOfOffice, OutOfOfficeId>(mainDbContext), IOutOfOfficeRepository
{
    public async Task<OutOfOffice[]> GetForUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(ooo => ooo.UserId == userId)
            .OrderBy(ooo => ooo.StartDate)
            .ThenBy(ooo => ooo.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OutOfOffice[]> GetActiveForUserUnfilteredAsync(TenantId tenantId, UserId userId, DateOnly windowStart, DateOnly windowEnd, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(ooo => ooo.TenantId == tenantId)
            .Where(ooo => ooo.UserId == userId)
            .Where(ooo => ooo.StartDate <= windowEnd && ooo.EndDate >= windowStart)
            .ToArrayAsync(cancellationToken);
    }
}

using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Schedules.Domain;

public interface ITravelScheduleRepository : ICrudRepository<TravelSchedule, TravelScheduleId>
{
    Task<TravelSchedule[]> GetForUserAsync(UserId userId, CancellationToken cancellationToken);

    Task<TravelSchedule[]> GetActiveForUserUnfilteredAsync(TenantId tenantId, UserId userId, DateOnly windowStart, DateOnly windowEnd, CancellationToken cancellationToken);
}

public sealed class TravelScheduleRepository(MainDbContext mainDbContext)
    : RepositoryBase<TravelSchedule, TravelScheduleId>(mainDbContext), ITravelScheduleRepository
{
    public async Task<TravelSchedule[]> GetForUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(travel => travel.UserId == userId)
            .OrderBy(travel => travel.StartDate)
            .ThenBy(travel => travel.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<TravelSchedule[]> GetActiveForUserUnfilteredAsync(TenantId tenantId, UserId userId, DateOnly windowStart, DateOnly windowEnd, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(travel => travel.TenantId == tenantId)
            .Where(travel => travel.UserId == userId)
            .Where(travel => travel.StartDate <= windowEnd && travel.EndDate >= windowStart)
            .ToArrayAsync(cancellationToken);
    }
}

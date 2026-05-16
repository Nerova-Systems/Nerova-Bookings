using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Schedules.Domain;

public interface IScheduleRepository : ICrudRepository<Schedule, ScheduleId>, ISoftDeletableRepository<Schedule, ScheduleId>
{
    Task<Schedule[]> GetForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken);

    Task<Schedule?> GetDefaultForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken);

    Task<Schedule[]> GetDefaultCandidatesForOwnerAsync(UserId ownerUserId, ScheduleId? excludedScheduleId, CancellationToken cancellationToken);
}

public sealed class ScheduleRepository(MainDbContext mainDbContext)
    : SoftDeletableRepositoryBase<Schedule, ScheduleId>(mainDbContext), IScheduleRepository
{
    public async Task<Schedule[]> GetForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(schedule => schedule.OwnerUserId == ownerUserId)
            .OrderBy(schedule => schedule.Name)
            .ThenBy(schedule => schedule.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Schedule?> GetDefaultForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(schedule => schedule.OwnerUserId == ownerUserId)
            .Where(schedule => schedule.IsDefault)
            .OrderBy(schedule => schedule.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Schedule[]> GetDefaultCandidatesForOwnerAsync(UserId ownerUserId, ScheduleId? excludedScheduleId, CancellationToken cancellationToken)
    {
        var schedules = DbSet
            .Where(schedule => schedule.OwnerUserId == ownerUserId)
            .Where(schedule => schedule.IsDefault);

        if (excludedScheduleId is not null)
        {
            schedules = schedules.Where(schedule => schedule.Id != excludedScheduleId);
        }

        return await schedules.ToArrayAsync(cancellationToken);
    }
}

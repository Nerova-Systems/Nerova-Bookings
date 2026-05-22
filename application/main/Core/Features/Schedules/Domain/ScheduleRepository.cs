using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Schedules.Domain;

public interface IScheduleRepository : ICrudRepository<Schedule, ScheduleId>, ISoftDeletableRepository<Schedule, ScheduleId>
{
    Task<Schedule[]> GetForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);

    Task<Schedule?> GetPublicByIdUnfilteredAsync(TenantId tenantId, UserId ownerUserId, ScheduleId scheduleId, CancellationToken cancellationToken);

    Task<Schedule?> GetDefaultForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);

    Task<Schedule[]> GetDefaultCandidatesForOwnerAsync(UserId ownerUserId, TenantId? teamId, ScheduleId? excludedScheduleId, CancellationToken cancellationToken);
}

public sealed class ScheduleRepository(MainDbContext mainDbContext)
    : SoftDeletableRepositoryBase<Schedule, ScheduleId>(mainDbContext), IScheduleRepository
{
    public async Task<Schedule[]> GetForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.Where(schedule => schedule.TeamId == teamId)
            : DbSet.Where(schedule => schedule.OwnerUserId == ownerUserId && schedule.TeamId == null);

        return await query
            .OrderBy(schedule => schedule.Name)
            .ThenBy(schedule => schedule.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Schedule?> GetPublicByIdUnfilteredAsync(TenantId tenantId, UserId ownerUserId, ScheduleId scheduleId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(schedule => schedule.TenantId == tenantId)
            .Where(schedule => schedule.OwnerUserId == ownerUserId)
            .Where(schedule => schedule.DeletedAt == null)
            .Where(schedule => schedule.Id == scheduleId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Schedule?> GetDefaultForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.Where(schedule => schedule.TeamId == teamId)
            : DbSet.Where(schedule => schedule.OwnerUserId == ownerUserId && schedule.TeamId == null);

        return await query
            .Where(schedule => schedule.IsDefault)
            .OrderBy(schedule => schedule.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Schedule[]> GetDefaultCandidatesForOwnerAsync(UserId ownerUserId, TenantId? teamId, ScheduleId? excludedScheduleId, CancellationToken cancellationToken)
    {
        var schedules = teamId is not null
            ? DbSet.Where(schedule => schedule.TeamId == teamId)
            : DbSet.Where(schedule => schedule.OwnerUserId == ownerUserId && schedule.TeamId == null);

        schedules = schedules.Where(schedule => schedule.IsDefault);

        if (excludedScheduleId is not null)
        {
            schedules = schedules.Where(schedule => schedule.Id != excludedScheduleId);
        }

        return await schedules.ToArrayAsync(cancellationToken);
    }
}

using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Scheduling.Domain;

public interface ISchedulingProfileRepository : ICrudRepository<SchedulingProfile, SchedulingProfileId>, ISoftDeletableRepository<SchedulingProfile, SchedulingProfileId>
{
    Task<SchedulingProfile?> GetForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);

    Task<SchedulingProfile?> GetByHandleAsync(string handle, CancellationToken cancellationToken);

    /// <summary>
    ///     Looks up a public scheduling profile without tenant query filters because anonymous public booker requests do
    ///     not carry tenant context. Callers must use the returned profile tenant and owner to scope subsequent reads.
    /// </summary>
    Task<SchedulingProfile?> GetByHandleUnfilteredAsync(string handle, CancellationToken cancellationToken);

    Task<bool> HandleExistsAsync(string handle, UserId? excludedOwnerUserId, CancellationToken cancellationToken);
}

public sealed class SchedulingProfileRepository(MainDbContext mainDbContext)
    : SoftDeletableRepositoryBase<SchedulingProfile, SchedulingProfileId>(mainDbContext), ISchedulingProfileRepository
{
    public async Task<SchedulingProfile?> GetForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.Where(profile => profile.TeamId == teamId)
            : DbSet.Where(profile => profile.OwnerUserId == ownerUserId && profile.TeamId == null);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SchedulingProfile?> GetByHandleAsync(string handle, CancellationToken cancellationToken)
    {
        var normalizedHandle = SchedulingProfile.NormalizeHandle(handle);
        return await DbSet
            .Where(profile => profile.Handle == normalizedHandle)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SchedulingProfile?> GetByHandleUnfilteredAsync(string handle, CancellationToken cancellationToken)
    {
        var normalizedHandle = SchedulingProfile.NormalizeHandle(handle);
        return await DbSet
            .IgnoreQueryFilters()
            .Where(profile => profile.DeletedAt == null)
            .Where(profile => profile.Handle == normalizedHandle)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HandleExistsAsync(string handle, UserId? excludedOwnerUserId, CancellationToken cancellationToken)
    {
        var normalizedHandle = SchedulingProfile.NormalizeHandle(handle);
        var profiles = DbSet
            .IgnoreQueryFilters()
            .Where(profile => profile.DeletedAt == null)
            .Where(profile => profile.Handle == normalizedHandle);

        if (excludedOwnerUserId is not null)
        {
            profiles = profiles.Where(profile => profile.OwnerUserId != excludedOwnerUserId);
        }

        return await profiles.AnyAsync(cancellationToken);
    }
}

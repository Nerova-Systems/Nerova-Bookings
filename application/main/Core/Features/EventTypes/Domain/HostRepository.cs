using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.EventTypes.Domain;

public interface IHostRepository : ICrudRepository<Host, HostId>
{
    Task<Host[]> GetForEventTypeAsync(EventTypeId eventTypeId, CancellationToken cancellationToken);

    /// <summary>
    ///     Retrieves hosts ignoring the tenant query filter. Used by anonymous public endpoints
    ///     (public slots and public bookings) where no execution-context tenant is available.
    /// </summary>
    Task<Host[]> GetForEventTypeUnfilteredAsync(EventTypeId eventTypeId, CancellationToken cancellationToken);

    Task<Host?> GetByEventTypeAndUserAsync(EventTypeId eventTypeId, UserId userId, CancellationToken cancellationToken);

    /// <summary>Distinct user ids that appear as a host on any event type belonging to the given team.</summary>
    Task<UserId[]> GetDistinctUserIdsForTeamAsync(TenantId teamId, CancellationToken cancellationToken);
}

public sealed class HostRepository(MainDbContext mainDbContext)
    : RepositoryBase<Host, HostId>(mainDbContext), IHostRepository
{
    public async Task<Host[]> GetForEventTypeAsync(EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(host => host.EventTypeId == eventTypeId)
            .OrderBy(host => host.Priority)
            .ThenBy(host => host.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Host[]> GetForEventTypeUnfilteredAsync(EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(host => host.EventTypeId == eventTypeId)
            .OrderBy(host => host.Priority)
            .ThenBy(host => host.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Host?> GetByEventTypeAndUserAsync(EventTypeId eventTypeId, UserId userId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(host => host.EventTypeId == eventTypeId && host.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserId[]> GetDistinctUserIdsForTeamAsync(TenantId teamId, CancellationToken cancellationToken)
    {
        var eventTypeIds = Context.Set<EventType>()
            .Where(eventType => eventType.TeamId == teamId)
            .Select(eventType => eventType.Id);

        return await DbSet
            .Where(host => eventTypeIds.Contains(host.EventTypeId))
            .Select(host => host.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }
}

using Main.Database;
using Main.Features.Schedules.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.EventTypes.Domain;

public interface IEventTypeRepository : ICrudRepository<EventType, EventTypeId>, ISoftDeletableRepository<EventType, EventTypeId>
{
    Task<EventType[]> GetForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);

    Task<EventType?> GetPublicBySlugUnfilteredAsync(TenantId tenantId, UserId ownerUserId, string slug, CancellationToken cancellationToken);

    Task<bool> ExistsForScheduleAsync(UserId ownerUserId, ScheduleId scheduleId, CancellationToken cancellationToken);

    Task<bool> SlugExistsForOwnerAsync(UserId ownerUserId, string slug, EventTypeId? excludedEventTypeId, CancellationToken cancellationToken);

    /// <summary>Returns all non-deleted child replicas for the given parent template.</summary>
    Task<EventType[]> GetChildrenAsync(EventTypeId parentId, CancellationToken cancellationToken);

    /// <summary>Returns the child replica belonging to the given parent and member, or null if not assigned.</summary>
    Task<EventType?> GetChildByParentAndMemberAsync(EventTypeId parentId, UserId memberUserId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all event types the caller can see for grouping by viewer scope:
    ///     personal (TeamId == null AND OwnerUserId == caller) plus team-scoped event types
    ///     where the caller is the owner OR appears as a Host. Org-level event types are
    ///     not exposed (no main-SCS data to determine org membership).
    /// </summary>
    Task<EventType[]> GetForViewerAsync(UserId callerUserId, CancellationToken cancellationToken);

    /// <summary>Returns event types matching the given ids (no soft-deletes).</summary>
    Task<EventType[]> GetByIdsAsync(EventTypeId[] ids, CancellationToken cancellationToken);
}

public sealed class EventTypeRepository(MainDbContext mainDbContext)
    : SoftDeletableRepositoryBase<EventType, EventTypeId>(mainDbContext), IEventTypeRepository
{
    public async Task<EventType[]> GetForOwnerAsync(UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.Where(eventType => eventType.TeamId == teamId)
            : DbSet.Where(eventType => eventType.OwnerUserId == ownerUserId && eventType.TeamId == null);

        return await query
            .OrderBy(eventType => eventType.Title)
            .ThenBy(eventType => eventType.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<EventType?> GetPublicBySlugUnfilteredAsync(TenantId tenantId, UserId ownerUserId, string slug, CancellationToken cancellationToken)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        return await DbSet
            .IgnoreQueryFilters()
            .Where(eventType => eventType.TenantId == tenantId)
            .Where(eventType => eventType.OwnerUserId == ownerUserId)
            .Where(eventType => eventType.DeletedAt == null)
            .Where(eventType => eventType.Slug == normalizedSlug)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsForScheduleAsync(UserId ownerUserId, ScheduleId scheduleId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(eventType => eventType.OwnerUserId == ownerUserId)
            .Where(eventType => eventType.ScheduleId == scheduleId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> SlugExistsForOwnerAsync(UserId ownerUserId, string slug, EventTypeId? excludedEventTypeId, CancellationToken cancellationToken)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var eventTypes = DbSet
            .Where(eventType => eventType.OwnerUserId == ownerUserId)
            .Where(eventType => eventType.Slug == normalizedSlug);

        if (excludedEventTypeId is not null)
        {
            eventTypes = eventTypes.Where(eventType => eventType.Id != excludedEventTypeId);
        }

        return await eventTypes.AnyAsync(cancellationToken);
    }

    public async Task<EventType[]> GetChildrenAsync(EventTypeId parentId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(eventType => eventType.ParentEventTypeId == parentId)
            .OrderBy(eventType => eventType.OwnerUserId)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<EventType?> GetChildByParentAndMemberAsync(EventTypeId parentId, UserId memberUserId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(eventType => eventType.ParentEventTypeId == parentId && eventType.OwnerUserId == memberUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EventType[]> GetForViewerAsync(UserId callerUserId, CancellationToken cancellationToken)
    {
        // Personal: caller owns and not team-scoped.
        // Team:     caller owns OR caller is a host (Hosts table) on a team-scoped event type.
        // Org-level event types are deferred: main has no membership data to filter on.
        var hostedEventTypeIds = Context.Set<Host>()
            .Where(host => host.UserId == callerUserId)
            .Select(host => host.EventTypeId);

        return await DbSet
            .Where(eventType =>
                (eventType.TeamId == null && eventType.OwnerUserId == callerUserId) ||
                (eventType.TeamId != null && (eventType.OwnerUserId == callerUserId || hostedEventTypeIds.Contains(eventType.Id)))
            )
            .OrderBy(eventType => eventType.TeamId == null ? 0 : 1)
            .ThenBy(eventType => eventType.Title)
            .ThenBy(eventType => eventType.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<EventType[]> GetByIdsAsync(EventTypeId[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0) return [];
        var idList = ids.ToList();
        return await DbSet.Where(eventType => idList.Contains(eventType.Id)).ToArrayAsync(cancellationToken);
    }
}

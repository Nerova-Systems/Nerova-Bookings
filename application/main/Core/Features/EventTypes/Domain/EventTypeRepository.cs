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
}

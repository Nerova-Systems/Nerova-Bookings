using Main.Database;
using Main.Features.Schedules.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.EventTypes.Domain;

public interface IEventTypeRepository : ICrudRepository<EventType, EventTypeId>, ISoftDeletableRepository<EventType, EventTypeId>
{
    Task<EventType[]> GetForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken);

    Task<EventType?> GetPublicBySlugUnfilteredAsync(TenantId tenantId, UserId ownerUserId, string slug, CancellationToken cancellationToken);

    Task<bool> ExistsForScheduleAsync(UserId ownerUserId, ScheduleId scheduleId, CancellationToken cancellationToken);

    Task<bool> SlugExistsForOwnerAsync(UserId ownerUserId, string slug, EventTypeId? excludedEventTypeId, CancellationToken cancellationToken);
}

public sealed class EventTypeRepository(MainDbContext mainDbContext)
    : SoftDeletableRepositoryBase<EventType, EventTypeId>(mainDbContext), IEventTypeRepository
{
    public async Task<EventType[]> GetForOwnerAsync(UserId ownerUserId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(eventType => eventType.OwnerUserId == ownerUserId)
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
}

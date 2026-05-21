using Main.Database;
using Main.Features.EventTypes.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.Scheduling.Domain;

public interface IBookingRepository : IAppendRepository<Booking, BookingId>
{
    void Update(Booking booking);

    Task<Booking[]> GetForOwnerRangeUnfilteredAsync(TenantId tenantId, UserId ownerUserId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    Task<int> CountForEventTypeSlotUnfilteredAsync(TenantId tenantId, EventTypeId eventTypeId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    Task<BookingWithEventType[]> GetForOwnerWithEventTypesAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);

    Task<BookingWithEventType?> GetForOwnerWithEventTypeAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, BookingId bookingId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all bookings for the specified scope (solo or team) without date filtering.
    ///     Date-range narrowing is performed in memory by callers (SQLite EF Core cannot translate
    ///     <see cref="DateTimeOffset" /> range comparisons to SQL).
    /// </summary>
    Task<Booking[]> GetForScopeAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all bookings joined with their event types for the specified scope.
    ///     The soft-delete filter on <see cref="EventType" /> is disabled so bookings belonging
    ///     to soft-deleted event types are still included in analytics.
    ///     Date-range narrowing is performed in memory by callers.
    /// </summary>
    Task<BookingWithEventType[]> GetForScopeWithEventTypesUnfilteredAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);
}

public sealed record BookingWithEventType(Booking Booking, EventType EventType);

public sealed class BookingRepository(MainDbContext mainDbContext)
    : RepositoryBase<Booking, BookingId>(mainDbContext), IBookingRepository
{
    public async Task<Booking[]> GetForOwnerRangeUnfilteredAsync(TenantId tenantId, UserId ownerUserId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        var bookings = await DbSet
            .IgnoreQueryFilters()
            .Where(booking => booking.TenantId == tenantId)
            .Where(booking => booking.OwnerUserId == ownerUserId)
            .ToArrayAsync(cancellationToken);

        return bookings
            .Where(booking => booking.StartTime < endTime && booking.EndTime > startTime)
            .OrderBy(booking => booking.StartTime)
            .ThenBy(booking => booking.Id)
            .ToArray();
    }

    public async Task<int> CountForEventTypeSlotUnfilteredAsync(TenantId tenantId, EventTypeId eventTypeId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken)
    {
        var bookings = await DbSet
            .IgnoreQueryFilters()
            .Where(booking => booking.TenantId == tenantId)
            .Where(booking => booking.EventTypeId == eventTypeId)
            .ToArrayAsync(cancellationToken);

        return bookings.Count(booking => booking.StartTime == startTime && booking.EndTime == endTime);
    }

    public async Task<BookingWithEventType[]> GetForOwnerWithEventTypesAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.AsNoTracking().Where(booking => booking.TenantId == tenantId && booking.TeamId == teamId)
            : DbSet.AsNoTracking().Where(booking => booking.TenantId == tenantId && booking.OwnerUserId == ownerUserId && booking.TeamId == null);

        return await query
            .Join(
                Context.Set<EventType>().IgnoreQueryFilters().AsNoTracking(),
                booking => booking.EventTypeId,
                eventType => eventType.Id,
                (booking, eventType) => new BookingWithEventType(booking, eventType)
            )
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Booking[]> GetForScopeAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.AsNoTracking().Where(b => b.TenantId == tenantId && b.TeamId == teamId)
            : DbSet.AsNoTracking().Where(b => b.TenantId == tenantId && b.OwnerUserId == ownerUserId && b.TeamId == null);

        return await query.ToArrayAsync(cancellationToken);
    }

    public async Task<BookingWithEventType?> GetForOwnerWithEventTypeAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, BookingId bookingId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.AsTracking().Where(booking => booking.TenantId == tenantId && booking.TeamId == teamId && booking.Id == bookingId)
            : DbSet.AsTracking().Where(booking => booking.TenantId == tenantId && booking.OwnerUserId == ownerUserId && booking.TeamId == null && booking.Id == bookingId);

        return await query
            .Join(
                Context.Set<EventType>().IgnoreQueryFilters().AsNoTracking(),
                booking => booking.EventTypeId,
                eventType => eventType.Id,
                (booking, eventType) => new BookingWithEventType(booking, eventType)
            )
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    ///     Retrieves bookings with their event types for the specified scope.
    ///     The soft-delete query filter on <see cref="EventType" /> is disabled so bookings for
    ///     soft-deleted event types are still returned.
    /// </summary>
    public async Task<BookingWithEventType[]> GetForScopeWithEventTypesUnfilteredAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken)
    {
        var query = teamId is not null
            ? DbSet.AsNoTracking().Where(b => b.TenantId == tenantId && b.TeamId == teamId)
            : DbSet.AsNoTracking().Where(b => b.TenantId == tenantId && b.OwnerUserId == ownerUserId && b.TeamId == null);

        return await query
            .Join(
                Context.Set<EventType>().IgnoreQueryFilters([QueryFilterNames.SoftDelete]).AsNoTracking(),
                booking => booking.EventTypeId,
                eventType => eventType.Id,
                (booking, eventType) => new BookingWithEventType(booking, eventType)
            )
            .ToArrayAsync(cancellationToken);
    }
}

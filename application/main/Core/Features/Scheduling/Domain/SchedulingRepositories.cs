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

    Task<Booking[]> GetForEventTypeUnfilteredAsync(TenantId tenantId, EventTypeId eventTypeId, CancellationToken cancellationToken);

    Task<int> CountForEventTypeSlotUnfilteredAsync(TenantId tenantId, EventTypeId eventTypeId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    Task<BookingWithEventType[]> GetForOwnerWithEventTypesAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, CancellationToken cancellationToken);

    Task<BookingWithEventType?> GetForOwnerWithEventTypeAsync(TenantId tenantId, UserId ownerUserId, TenantId? teamId, BookingId bookingId, CancellationToken cancellationToken);

    /// <summary>
    ///     Tenant-scoped lookup of a single booking joined with its event type. Unlike
    ///     <see cref="GetForOwnerWithEventTypeAsync" /> this method does not filter by owner or
    ///     team and is intended for callers (Admin/Owner) authorised to operate on any booking
    ///     within their tenant.
    /// </summary>
    Task<BookingWithEventType?> GetByIdInTenantWithEventTypeAsync(TenantId tenantId, BookingId bookingId, CancellationToken cancellationToken);

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

    /// <summary>
    ///     Returns bookings grouped by owner for multiple user IDs. Used by collective scheduling to check
    ///     availability across all hosts simultaneously.
    /// </summary>
    Task<IReadOnlyDictionary<UserId, Booking[]>> GetForMultipleOwnersRangeAsync(TenantId tenantId, IReadOnlyList<UserId> ownerUserIds, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    /// <summary>
    ///     Cross-tenant lookup for the Paystack webhook handler — the webhook is anonymous and
    ///     does not carry tenant context, so we resolve the booking via its globally-unique
    ///     payment reference. Tenant scoping is then derived from <see cref="Booking.TenantId" />.
    /// </summary>
    Task<Booking?> GetByPaymentReferenceUnfilteredAsync(string paymentReference, CancellationToken cancellationToken);

    /// <summary>
    ///     Polls for bookings whose payment-pending hold has expired. Used by the release job;
    ///     intentionally unfiltered so it works without an execution context.
    /// </summary>
    Task<Booking[]> GetExpiredUnpaidBookingsUnfilteredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    ///     Polls for pending after-session payments whose payment-state-changed timestamp is
    ///     older than the supplied cutoff and which have not yet been reminded. Used by the
    ///     payment-reminder job; intentionally unfiltered so it works without an execution context.
    /// </summary>
    Task<Booking[]> GetPendingPaymentsForReminderUnfilteredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    ///     Cross-tenant lookup by id. Used by anonymous / background callers (WhatsApp Flow
    ///     dispatcher + TickerQ payment jobs) that do not carry tenant context.
    /// </summary>
    Task<Booking?> GetByIdUnfilteredAsync(BookingId bookingId, CancellationToken cancellationToken);
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

    /// <summary>
    ///     Retrieves bookings for an event type without applying tenant and soft-delete query filters.
    /// </summary>
    public async Task<Booking[]> GetForEventTypeUnfilteredAsync(TenantId tenantId, EventTypeId eventTypeId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(booking => booking.TenantId == tenantId)
            .Where(booking => booking.EventTypeId == eventTypeId)
            .OrderBy(booking => booking.StartTime)
            .ThenBy(booking => booking.Id)
            .ToArrayAsync(cancellationToken);
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

    public async Task<BookingWithEventType?> GetByIdInTenantWithEventTypeAsync(TenantId tenantId, BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet.AsTracking()
            .Where(booking => booking.TenantId == tenantId && booking.Id == bookingId)
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

    /// <summary>
    ///     Retrieves bookings grouped by owner for multiple user IDs. Used by collective scheduling to check
    ///     availability across all hosts simultaneously.
    ///     Date-range narrowing is performed in memory (SQLite EF Core cannot translate
    ///     <see cref="DateTimeOffset" /> range comparisons to SQL).
    /// </summary>
    public async Task<IReadOnlyDictionary<UserId, Booking[]>> GetForMultipleOwnersRangeAsync(
        TenantId tenantId,
        IReadOnlyList<UserId> ownerUserIds,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken)
    {
        var allBookings = await DbSet
            .IgnoreQueryFilters()
            .Where(booking => booking.TenantId == tenantId && ownerUserIds.Contains(booking.OwnerUserId))
            .ToArrayAsync(cancellationToken);

        return allBookings
            .Where(booking => booking.StartTime < endTime && booking.EndTime > startTime)
            .GroupBy(booking => booking.OwnerUserId)
            .ToDictionary(group => group.Key, group => group.OrderBy(b => b.StartTime).ToArray());
    }

    public async Task<Booking?> GetByPaymentReferenceUnfilteredAsync(string paymentReference, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .AsTracking()
            .SingleOrDefaultAsync(b => b.PaymentReference == paymentReference, cancellationToken);
    }

    public async Task<Booking[]> GetExpiredUnpaidBookingsUnfilteredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(b => b.PaymentStatus == BookingPaymentStatus.Pending
                        && b.PaymentStateChangedAt != null
                        && b.PaymentStateChangedAt < cutoff)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Booking[]> GetPendingPaymentsForReminderUnfilteredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(b => b.PaymentStatus == BookingPaymentStatus.Pending
                        && b.PaymentReminderSentAt == null
                        && b.PaymentStateChangedAt != null
                        && b.PaymentStateChangedAt < cutoff)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Booking?> GetByIdUnfilteredAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .AsTracking()
            .SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
    }
}

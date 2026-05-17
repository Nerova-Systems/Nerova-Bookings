using Main.Database;
using Main.Features.EventTypes.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Scheduling.Domain;

public interface IBookingRepository : IAppendRepository<Booking, BookingId>
{
    Task<Booking[]> GetForOwnerRangeUnfilteredAsync(TenantId tenantId, UserId ownerUserId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);

    Task<int> CountForEventTypeSlotUnfilteredAsync(TenantId tenantId, EventTypeId eventTypeId, DateTimeOffset startTime, DateTimeOffset endTime, CancellationToken cancellationToken);
}

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
}

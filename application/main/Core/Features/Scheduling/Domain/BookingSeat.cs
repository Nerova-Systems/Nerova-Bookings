using JetBrains.Annotations;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("seat")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingSeatId>))]
public sealed record BookingSeatId(string Value) : StronglyTypedUlid<BookingSeatId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A reserved seat on a seated booking. Each seat references the booking and the attendee
///     occupying it, with a per-booking unique reference UID used in attendee-facing links.
/// </summary>
public sealed class BookingSeat : AggregateRoot<BookingSeatId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private BookingSeat() : base(BookingSeatId.NewId())
    {
        BookingId = new BookingId(string.Empty);
        AttendeeId = new BookingAttendeeId(string.Empty);
        ReferenceUid = string.Empty;
    }

    private BookingSeat(TenantId tenantId, BookingId bookingId, BookingAttendeeId attendeeId, string referenceUid, string? data)
        : base(BookingSeatId.NewId())
    {
        TenantId = tenantId;
        BookingId = bookingId;
        AttendeeId = attendeeId;
        ReferenceUid = referenceUid.Trim();
        Data = data;
    }

    public BookingId BookingId { get; init; }

    public BookingAttendeeId AttendeeId { get; init; }

    public string ReferenceUid { get; init; }

    public string? Data { get; init; }

    public TenantId TenantId { get; } = new(0);

    public static BookingSeat Create(TenantId tenantId, BookingId bookingId, BookingAttendeeId attendeeId, string referenceUid, string? data = null)
    {
        return new BookingSeat(tenantId, bookingId, attendeeId, referenceUid, data);
    }
}

public sealed class BookingSeatConfiguration : IEntityTypeConfiguration<BookingSeat>
{
    public void Configure(EntityTypeBuilder<BookingSeat> builder)
    {
        builder.MapStronglyTypedUuid<BookingSeat, BookingSeatId>(seat => seat.Id);
        builder.MapStronglyTypedLongId<BookingSeat, TenantId>(seat => seat.TenantId);
        builder.MapStronglyTypedUuid<BookingSeat, BookingId>(seat => seat.BookingId);
        builder.MapStronglyTypedUuid<BookingSeat, BookingAttendeeId>(seat => seat.AttendeeId);

        builder.Property(seat => seat.Data).HasColumnType("jsonb");

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(seat => seat.BookingId)
            .HasPrincipalKey(booking => booking.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(seat => seat.BookingId);
        builder.HasIndex(seat => new { seat.BookingId, seat.ReferenceUid }).IsUnique();
    }
}

public interface IBookingSeatRepository : ICrudRepository<BookingSeat, BookingSeatId>
{
    Task<BookingSeat[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);

    Task<BookingSeat?> GetByReferenceAsync(BookingId bookingId, string referenceUid, CancellationToken cancellationToken);
}

public sealed class BookingSeatRepository(MainDbContext mainDbContext)
    : RepositoryBase<BookingSeat, BookingSeatId>(mainDbContext), IBookingSeatRepository
{
    public async Task<BookingSeat[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking()
            .Where(seat => seat.BookingId == bookingId)
            .OrderBy(seat => seat.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<BookingSeat?> GetByReferenceAsync(BookingId bookingId, string referenceUid, CancellationToken cancellationToken)
    {
        return await DbSet.AsTracking()
            .Where(seat => seat.BookingId == bookingId && seat.ReferenceUid == referenceUid)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

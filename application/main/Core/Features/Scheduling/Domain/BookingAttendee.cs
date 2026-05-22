using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("att")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingAttendeeId>))]
public sealed record BookingAttendeeId(string Value) : StronglyTypedUlid<BookingAttendeeId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     An attendee on a <see cref="Booking" />. Multiple attendees are supported (e.g., seated events,
///     additional guests added after creation). Stored as a separate aggregate so that lists and
///     no-show flags can be queried without rehydrating the parent booking.
/// </summary>
public sealed class BookingAttendee : AggregateRoot<BookingAttendeeId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private BookingAttendee() : base(BookingAttendeeId.NewId())
    {
        BookingId = new BookingId(string.Empty);
        Name = string.Empty;
        Email = string.Empty;
        TimeZone = string.Empty;
        Locale = string.Empty;
    }

    private BookingAttendee(TenantId tenantId, BookingId bookingId, string name, string email, string timeZone, string locale)
        : base(BookingAttendeeId.NewId())
    {
        TenantId = tenantId;
        BookingId = bookingId;
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        TimeZone = timeZone.Trim();
        Locale = locale.Trim();
    }

    public BookingId BookingId { get; private set; }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public string TimeZone { get; private set; }

    public string Locale { get; private set; }

    public bool NoShow { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public void MarkNoShow(bool value)
    {
        NoShow = value;
    }

    public static BookingAttendee Create(TenantId tenantId, BookingId bookingId, string name, string email, string timeZone, string locale)
    {
        return new BookingAttendee(tenantId, bookingId, name, email, timeZone, locale);
    }
}

public sealed class BookingAttendeeConfiguration : IEntityTypeConfiguration<BookingAttendee>
{
    public void Configure(EntityTypeBuilder<BookingAttendee> builder)
    {
        builder.MapStronglyTypedUuid<BookingAttendee, BookingAttendeeId>(attendee => attendee.Id);
        builder.MapStronglyTypedLongId<BookingAttendee, TenantId>(attendee => attendee.TenantId);
        builder.MapStronglyTypedUuid<BookingAttendee, BookingId>(attendee => attendee.BookingId);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(attendee => attendee.BookingId)
            .HasPrincipalKey(booking => booking.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(attendee => attendee.BookingId);
        builder.HasIndex(attendee => new { attendee.TenantId, attendee.Email });
    }
}

public interface IBookingAttendeeRepository : ICrudRepository<BookingAttendee, BookingAttendeeId>
{
    Task<BookingAttendee[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);

    Task<BookingAttendee[]> GetForBookingsAsync(BookingId[] bookingIds, CancellationToken cancellationToken);
}

public sealed class BookingAttendeeRepository(MainDbContext mainDbContext)
    : RepositoryBase<BookingAttendee, BookingAttendeeId>(mainDbContext), IBookingAttendeeRepository
{
    public async Task<BookingAttendee[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking()
            .Where(attendee => attendee.BookingId == bookingId)
            .OrderBy(attendee => attendee.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<BookingAttendee[]> GetForBookingsAsync(BookingId[] bookingIds, CancellationToken cancellationToken)
    {
        if (bookingIds.Length == 0) return [];
        return await DbSet.AsNoTracking()
            .Where(attendee => bookingIds.Contains(attendee.BookingId))
            .OrderBy(attendee => attendee.Id)
            .ToArrayAsync(cancellationToken);
    }
}

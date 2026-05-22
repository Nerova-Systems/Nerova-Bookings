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

[IdPrefix("bhe")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingHistoryEntryId>))]
public sealed record BookingHistoryEntryId(string Value) : StronglyTypedUlid<BookingHistoryEntryId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Append-only audit-trail entry for a <see cref="Booking" />. Captures lifecycle events
///     (created, confirmed, rejected, rescheduled, cancelled, no-show, location-changed,
///     guest-added, reassigned, rated, seat-reserved, seat-released) for display in the booking
///     details sheet.
/// </summary>
public sealed class BookingHistoryEntry : AggregateRoot<BookingHistoryEntryId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private BookingHistoryEntry() : base(BookingHistoryEntryId.NewId())
    {
        BookingId = new BookingId(string.Empty);
    }

    private BookingHistoryEntry(TenantId tenantId, BookingId bookingId, BookingHistoryEventType eventType, UserId? actorUserId, string? payloadJson, DateTimeOffset occurredAt)
        : base(BookingHistoryEntryId.NewId())
    {
        TenantId = tenantId;
        BookingId = bookingId;
        EventType = eventType;
        ActorUserId = actorUserId;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
    }

    public BookingId BookingId { get; private set; }

    public BookingHistoryEventType EventType { get; private set; }

    public UserId? ActorUserId { get; private set; }

    public string? PayloadJson { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static BookingHistoryEntry Create(TenantId tenantId, BookingId bookingId, BookingHistoryEventType eventType, DateTimeOffset occurredAt, UserId? actorUserId = null, string? payloadJson = null)
    {
        return new BookingHistoryEntry(tenantId, bookingId, eventType, actorUserId, payloadJson, occurredAt);
    }
}

public sealed class BookingHistoryEntryConfiguration : IEntityTypeConfiguration<BookingHistoryEntry>
{
    public void Configure(EntityTypeBuilder<BookingHistoryEntry> builder)
    {
        builder.MapStronglyTypedUuid<BookingHistoryEntry, BookingHistoryEntryId>(entry => entry.Id);
        builder.MapStronglyTypedLongId<BookingHistoryEntry, TenantId>(entry => entry.TenantId);
        builder.MapStronglyTypedUuid<BookingHistoryEntry, BookingId>(entry => entry.BookingId);
        builder.MapStronglyTypedNullableId<BookingHistoryEntry, UserId, string>(entry => entry.ActorUserId);

        builder.Property(entry => entry.PayloadJson).HasColumnType("jsonb");

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(entry => entry.BookingId)
            .HasPrincipalKey(booking => booking.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(entry => entry.BookingId);
        builder.HasIndex(entry => entry.OccurredAt);
    }
}

public interface IBookingHistoryEntryRepository : IAppendRepository<BookingHistoryEntry, BookingHistoryEntryId>
{
    Task<BookingHistoryEntry[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);

    Task<(BookingHistoryEntry[] Entries, int TotalCount)> GetForBookingPagedAsync(BookingId bookingId, int pageOffset, int pageSize, CancellationToken cancellationToken);
}

public sealed class BookingHistoryEntryRepository(MainDbContext mainDbContext)
    : RepositoryBase<BookingHistoryEntry, BookingHistoryEntryId>(mainDbContext), IBookingHistoryEntryRepository
{
    public async Task<BookingHistoryEntry[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking()
            .Where(entry => entry.BookingId == bookingId)
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<(BookingHistoryEntry[] Entries, int TotalCount)> GetForBookingPagedAsync(BookingId bookingId, int pageOffset, int pageSize, CancellationToken cancellationToken)
    {
        var query = DbSet.AsNoTracking().Where(entry => entry.BookingId == bookingId);
        var total = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .Skip(pageOffset)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return (entries, total);
    }
}

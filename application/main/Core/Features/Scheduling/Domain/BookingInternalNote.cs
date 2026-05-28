using JetBrains.Annotations;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("bnote")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingInternalNoteId>))]
public sealed record BookingInternalNoteId(string Value) : StronglyTypedUlid<BookingInternalNoteId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A host-private note attached to a <see cref="Booking" />. Only visible to authenticated
///     hosts / team members of the booking — never exposed to attendees.
/// </summary>
public sealed class BookingInternalNote : AggregateRoot<BookingInternalNoteId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private BookingInternalNote() : base(BookingInternalNoteId.NewId())
    {
        BookingId = new BookingId(string.Empty);
        AuthorUserId = new UserId(string.Empty);
        Body = string.Empty;
    }

    private BookingInternalNote(TenantId tenantId, BookingId bookingId, UserId authorUserId, string body)
        : base(BookingInternalNoteId.NewId())
    {
        TenantId = tenantId;
        BookingId = bookingId;
        AuthorUserId = authorUserId;
        Body = body.Trim();
    }

    public BookingId BookingId { get; init; }

    public UserId AuthorUserId { get; init; }

    public string Body { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public void UpdateBody(string body)
    {
        Body = body.Trim();
    }

    public static BookingInternalNote Create(TenantId tenantId, BookingId bookingId, UserId authorUserId, string body)
    {
        return new BookingInternalNote(tenantId, bookingId, authorUserId, body);
    }
}

public sealed class BookingInternalNoteConfiguration : IEntityTypeConfiguration<BookingInternalNote>
{
    public void Configure(EntityTypeBuilder<BookingInternalNote> builder)
    {
        builder.MapStronglyTypedUuid<BookingInternalNote, BookingInternalNoteId>(note => note.Id);
        builder.MapStronglyTypedLongId<BookingInternalNote, TenantId>(note => note.TenantId);
        builder.MapStronglyTypedUuid<BookingInternalNote, BookingId>(note => note.BookingId);
        builder.MapStronglyTypedUuid<BookingInternalNote, UserId>(note => note.AuthorUserId);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(note => note.BookingId)
            .HasPrincipalKey(booking => booking.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(note => note.BookingId);
    }
}

public interface IBookingInternalNoteRepository : ICrudRepository<BookingInternalNote, BookingInternalNoteId>
{
    Task<BookingInternalNote[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);

    Task<BookingInternalNote?> GetByIdForBookingAsync(BookingId bookingId, BookingInternalNoteId noteId, CancellationToken cancellationToken);
}

public sealed class BookingInternalNoteRepository(MainDbContext mainDbContext)
    : RepositoryBase<BookingInternalNote, BookingInternalNoteId>(mainDbContext), IBookingInternalNoteRepository
{
    public async Task<BookingInternalNote[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking()
            .Where(note => note.BookingId == bookingId)
            .OrderBy(note => note.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<BookingInternalNote?> GetByIdForBookingAsync(BookingId bookingId, BookingInternalNoteId noteId, CancellationToken cancellationToken)
    {
        return await DbSet.AsTracking()
            .Where(note => note.BookingId == bookingId && note.Id == noteId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}

using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.Apps.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.Scheduling.Domain;

[IdPrefix("bref")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingReferenceId>))]
public sealed record BookingReferenceId(string Value) : StronglyTypedUlid<BookingReferenceId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Cross-reference between a local <see cref="Booking" /> and the equivalent record in an
///     external app (Google Calendar event, Zoom meeting, …). Stores the provider's identifier
///     plus an optional canonical URL so the lifecycle handlers can update or cancel the remote
///     record when the local booking changes. One row per <c>(Booking, AppSlug)</c> tuple.
/// </summary>
public sealed class BookingReference : AggregateRoot<BookingReferenceId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private BookingReference() : base(BookingReferenceId.NewId())
    {
        BookingId = new BookingId(string.Empty);
        AppSlug = new AppSlug(string.Empty);
        ExternalId = string.Empty;
    }

    private BookingReference(
        TenantId tenantId,
        BookingId bookingId,
        AppSlug appSlug,
        string externalId,
        string? externalUrl
    ) : base(BookingReferenceId.NewId())
    {
        TenantId = tenantId;
        BookingId = bookingId;
        AppSlug = appSlug;
        ExternalId = externalId;
        ExternalUrl = externalUrl;
    }

    public TenantId TenantId { get; } = new(0);

    public BookingId BookingId { get; private set; }

    public AppSlug AppSlug { get; private set; }

    public string ExternalId { get; private set; }

    public string? ExternalUrl { get; private set; }

    public static BookingReference Create(TenantId tenantId, BookingId bookingId, AppSlug appSlug, string externalId, string? externalUrl = null)
    {
        if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentException("External id is required.", nameof(externalId));
        return new BookingReference(tenantId, bookingId, appSlug, externalId.Trim(), string.IsNullOrWhiteSpace(externalUrl) ? null : externalUrl.Trim());
    }

    public void UpdateExternalId(string externalId, string? externalUrl)
    {
        if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentException("External id is required.", nameof(externalId));
        ExternalId = externalId.Trim();
        ExternalUrl = string.IsNullOrWhiteSpace(externalUrl) ? null : externalUrl.Trim();
    }
}

public sealed class BookingReferenceConfiguration : IEntityTypeConfiguration<BookingReference>
{
    public void Configure(EntityTypeBuilder<BookingReference> builder)
    {
        builder.MapStronglyTypedUuid<BookingReference, BookingReferenceId>(reference => reference.Id);
        builder.MapStronglyTypedLongId<BookingReference, TenantId>(reference => reference.TenantId);
        builder.MapStronglyTypedUuid<BookingReference, BookingId>(reference => reference.BookingId);
        builder.MapStronglyTypedId<BookingReference, AppSlug, string>(reference => reference.AppSlug);

        builder.Property(reference => reference.ExternalId).HasMaxLength(400);
        builder.Property(reference => reference.ExternalUrl).HasMaxLength(1000);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(reference => reference.BookingId)
            .HasPrincipalKey(booking => booking.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(reference => new { reference.BookingId, reference.AppSlug }).IsUnique();
        builder.HasIndex(reference => reference.AppSlug);
    }
}

public interface IBookingReferenceRepository : ICrudRepository<BookingReference, BookingReferenceId>
{
    Task<BookingReference?> GetAsync(BookingId bookingId, AppSlug appSlug, CancellationToken cancellationToken);

    Task<BookingReference[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);
}

public sealed class BookingReferenceRepository(MainDbContext mainDbContext)
    : RepositoryBase<BookingReference, BookingReferenceId>(mainDbContext), IBookingReferenceRepository
{
    public async Task<BookingReference?> GetAsync(BookingId bookingId, AppSlug appSlug, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(reference => reference.BookingId == bookingId && reference.AppSlug == appSlug)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BookingReference[]> GetForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet
            .Where(reference => reference.BookingId == bookingId)
            .ToArrayAsync(cancellationToken);
    }
}

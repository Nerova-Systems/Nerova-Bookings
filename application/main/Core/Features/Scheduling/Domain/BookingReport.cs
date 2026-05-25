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

[IdPrefix("brep")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, BookingReportId>))]
public sealed record BookingReportId(string Value) : StronglyTypedUlid<BookingReportId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Reasons a host or team member may flag a <see cref="Booking" />. Mirrors typical
///     trust-and-safety taxonomies (spam, abuse, no-show, etc.). The enum is serialised as a
///     short uppercase string token in the persistence layer and over the wire.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingReportReasonCode
{
    Spam,
    Abuse,
    NoShow,
    IncorrectAttendee,
    Other
}

/// <summary>
///     A trust-and-safety report filed by a workspace member against a <see cref="Booking" />.
///     Reports are tenant-scoped, append-only, and surface to Admin/Owner via the
///     <c>GET /api/bookings/reports</c> endpoint. Members may file but not list.
/// </summary>
public sealed class BookingReport : AggregateRoot<BookingReportId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private BookingReport() : base(BookingReportId.NewId())
    {
        BookingId = new BookingId(string.Empty);
        ReportedByUserId = new UserId(string.Empty);
    }

    private BookingReport(TenantId tenantId, BookingId bookingId, UserId reportedByUserId, BookingReportReasonCode reasonCode, string? notes)
        : base(BookingReportId.NewId())
    {
        TenantId = tenantId;
        BookingId = bookingId;
        ReportedByUserId = reportedByUserId;
        ReasonCode = reasonCode;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public BookingId BookingId { get; private set; }

    public UserId ReportedByUserId { get; private set; }

    public BookingReportReasonCode ReasonCode { get; private set; }

    public string? Notes { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static BookingReport Create(TenantId tenantId, BookingId bookingId, UserId reportedByUserId, BookingReportReasonCode reasonCode, string? notes)
    {
        return new BookingReport(tenantId, bookingId, reportedByUserId, reasonCode, notes);
    }
}

public sealed class BookingReportConfiguration : IEntityTypeConfiguration<BookingReport>
{
    public void Configure(EntityTypeBuilder<BookingReport> builder)
    {
        builder.MapStronglyTypedUuid<BookingReport, BookingReportId>(report => report.Id);
        builder.MapStronglyTypedLongId<BookingReport, TenantId>(report => report.TenantId);
        builder.MapStronglyTypedUuid<BookingReport, BookingId>(report => report.BookingId);
        builder.MapStronglyTypedUuid<BookingReport, UserId>(report => report.ReportedByUserId);

        builder.Property(report => report.ReasonCode).HasConversion<string>().HasMaxLength(40);
        builder.Property(report => report.Notes).HasMaxLength(2000);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(report => report.BookingId)
            .HasPrincipalKey(booking => booking.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(report => new { report.TenantId, report.CreatedAt });
        builder.HasIndex(report => report.BookingId);
    }
}

public interface IBookingReportRepository : IAppendRepository<BookingReport, BookingReportId>
{
    Task<BookingReport[]> GetForTenantAsync(TenantId tenantId, int pageOffset, int pageSize, CancellationToken cancellationToken);

    Task<int> CountForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);
}

public sealed class BookingReportRepository(MainDbContext mainDbContext)
    : RepositoryBase<BookingReport, BookingReportId>(mainDbContext), IBookingReportRepository
{
    public async Task<BookingReport[]> GetForTenantAsync(TenantId tenantId, int pageOffset, int pageSize, CancellationToken cancellationToken)
    {
        // Sort client-side: SQLite (used in tests) cannot ORDER BY DateTimeOffset. The result set
        // is filtered by tenant up-front so volume stays bounded.
        var all = await DbSet.AsNoTracking()
            .Where(report => report.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);

        return all
            .OrderByDescending(report => report.CreatedAt)
            .ThenBy(report => report.Id.Value)
            .Skip(pageOffset)
            .Take(pageSize)
            .ToArray();
    }

    public async Task<int> CountForTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await DbSet.AsNoTracking()
            .Where(report => report.TenantId == tenantId)
            .CountAsync(cancellationToken);
    }
}

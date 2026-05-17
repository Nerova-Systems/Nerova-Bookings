using Main.Features.EventTypes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Scheduling.Domain;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.MapStronglyTypedUuid<Booking, BookingId>(booking => booking.Id);
        builder.MapStronglyTypedLongId<Booking, TenantId>(booking => booking.TenantId);
        builder.MapStronglyTypedUuid<Booking, UserId>(booking => booking.OwnerUserId);
        builder.MapStronglyTypedUuid<Booking, EventTypeId>(booking => booking.EventTypeId);

        builder.Property(booking => booking.BookerName).HasMaxLength(120);
        builder.Property(booking => booking.BookerEmail).HasMaxLength(320);
        builder.Property(booking => booking.TimeZone).HasMaxLength(100);
        builder.Property(booking => booking.Status).HasMaxLength(40);
        builder.Property(booking => booking.ResponsesJson).HasColumnType("jsonb");

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(booking => booking.EventTypeId)
            .HasPrincipalKey(eventType => eventType.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(booking => new { booking.TenantId, booking.OwnerUserId, booking.StartTime, booking.EndTime });
        builder.HasIndex(booking => new { booking.TenantId, booking.EventTypeId, booking.StartTime, booking.EndTime });
    }
}

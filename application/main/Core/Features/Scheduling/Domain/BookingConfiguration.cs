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
        builder.MapStronglyTypedNullableLongId<Booking, TenantId>(booking => booking.TeamId);
        builder.MapStronglyTypedUuid<Booking, UserId>(booking => booking.OwnerUserId);
        builder.MapStronglyTypedUuid<Booking, EventTypeId>(booking => booking.EventTypeId);
        builder.MapStronglyTypedNullableId<Booking, UserId, string>(booking => booking.ReassignByUserId);

        builder.Property(booking => booking.BookerName).HasMaxLength(120);
        builder.Property(booking => booking.BookerEmail).HasMaxLength(320);
        builder.Property(booking => booking.TimeZone).HasMaxLength(100);
        builder.Property(booking => booking.Status).HasMaxLength(40);
        builder.Property(booking => booking.ResponsesJson).HasColumnType("jsonb");

        builder.Property(booking => booking.CancellationReason).HasMaxLength(1000);
        builder.Property(booking => booking.RejectionReason).HasMaxLength(1000);
        builder.Property(booking => booking.ReassignReason).HasMaxLength(1000);
        builder.Property(booking => booking.FromRescheduleUid).HasMaxLength(64);
        builder.Property(booking => booking.CancelledByUserUid).HasMaxLength(64);
        builder.Property(booking => booking.RescheduledByUserUid).HasMaxLength(64);
        builder.Property(booking => booking.SmsReminderNumber).HasMaxLength(40);
        builder.Property(booking => booking.CalUid).HasMaxLength(255).HasColumnName("i_cal_uid");
        builder.Property(booking => booking.CalSequence).HasColumnName("i_cal_sequence");
        builder.Property(booking => booking.RatingFeedback).HasMaxLength(2000);
        builder.Property(booking => booking.OneTimePassword).HasMaxLength(64);
        builder.Property(booking => booking.CustomInputsJson).HasColumnType("jsonb");
        builder.Property(booking => booking.MetadataJson).HasColumnType("jsonb");
        builder.Property(booking => booking.LocationType).HasMaxLength(80);
        builder.Property(booking => booking.LocationValue).HasMaxLength(2000);

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(booking => booking.EventTypeId)
            .HasPrincipalKey(eventType => eventType.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(booking => new { booking.TenantId, booking.OwnerUserId, booking.StartTime, booking.EndTime });
        builder.HasIndex(booking => new { booking.TenantId, booking.EventTypeId, booking.StartTime, booking.EndTime });
        builder.HasIndex(booking => booking.TeamId);
        builder.HasIndex(booking => new { booking.TenantId, booking.CalUid }).IsUnique().HasDatabaseName("ix_bookings_tenant_id_i_cal_uid");
    }
}

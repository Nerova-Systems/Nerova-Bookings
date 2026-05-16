using Main.Features.Schedules.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.EventTypes.Domain;

public sealed class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.MapStronglyTypedUuid<EventType, EventTypeId>(eventType => eventType.Id);
        builder.MapStronglyTypedLongId<EventType, TenantId>(eventType => eventType.TenantId);
        builder.MapStronglyTypedUuid<EventType, UserId>(eventType => eventType.OwnerUserId);
        builder.MapStronglyTypedUuid<EventType, ScheduleId>(eventType => eventType.ScheduleId);

        builder.Property(eventType => eventType.Title).HasMaxLength(120);
        builder.Property(eventType => eventType.Slug).HasMaxLength(120);
        builder.Property(eventType => eventType.Description).HasMaxLength(1000);
        builder.Property(eventType => eventType.LocationType).HasMaxLength(80);
        builder.Property(eventType => eventType.LocationValue).HasMaxLength(500);

        builder.HasOne<Schedule>()
            .WithMany()
            .HasForeignKey(eventType => eventType.ScheduleId)
            .HasPrincipalKey(schedule => schedule.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(eventType => new { eventType.TenantId, eventType.OwnerUserId, eventType.Slug })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
        builder.HasIndex(eventType => new { eventType.TenantId, eventType.OwnerUserId, eventType.Title });
    }
}

using System.Text.Json;
using Main.Features.Schedules.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.EventTypes.Domain;

public sealed class EventTypeConfiguration : IEntityTypeConfiguration<EventType>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    private static readonly ValueComparer<string[]> StringArrayComparer = new(
        (left, right) => left != null && right != null && left.SequenceEqual(right),
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
        value => value.ToArray()
    );

    public void Configure(EntityTypeBuilder<EventType> builder)
    {
        builder.MapStronglyTypedUuid<EventType, EventTypeId>(eventType => eventType.Id);
        builder.MapStronglyTypedLongId<EventType, TenantId>(eventType => eventType.TenantId);
        builder.MapStronglyTypedNullableLongId<EventType, TenantId>(eventType => eventType.TeamId);
        builder.MapStronglyTypedUuid<EventType, UserId>(eventType => eventType.OwnerUserId);
        builder.MapStronglyTypedUuid<EventType, ScheduleId>(eventType => eventType.ScheduleId);
        builder.MapStronglyTypedNullableId<EventType, EventTypeId, string>(eventType => eventType.ParentEventTypeId);

        builder.Property(eventType => eventType.Title).HasMaxLength(120);
        builder.Property(eventType => eventType.Slug).HasMaxLength(120);
        builder.Property(eventType => eventType.Description).HasMaxLength(1000);
        builder.Property(eventType => eventType.LocationType).HasMaxLength(80);
        builder.Property(eventType => eventType.LocationValue).HasMaxLength(500);
        builder.Property(eventType => eventType.Settings)
            .HasColumnType("jsonb")
            .HasConversion(
                settings => JsonSerializer.Serialize(settings, JsonSerializerOptions),
                value => JsonSerializer.Deserialize<EventTypeSettings>(value, JsonSerializerOptions) ?? new EventTypeSettings()
            );
        builder.Property(eventType => eventType.UnlockedFields)
            .HasColumnType("jsonb")
            .HasConversion(
                fields => JsonSerializer.Serialize(fields, JsonSerializerOptions),
                value => JsonSerializer.Deserialize<string[]>(value, JsonSerializerOptions) ?? Array.Empty<string>()
            )
            .Metadata.SetValueComparer(StringArrayComparer);

        builder.Property(eventType => eventType.SchedulingType)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<SchedulingType>(v)
            );

        builder.HasOne<Schedule>()
            .WithMany()
            .HasForeignKey(eventType => eventType.ScheduleId)
            .HasPrincipalKey(schedule => schedule.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(eventType => new { eventType.TenantId, eventType.OwnerUserId, eventType.Slug })
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
        builder.HasIndex(eventType => new { eventType.TenantId, eventType.OwnerUserId, eventType.Title });
        builder.HasIndex(eventType => eventType.TeamId);
    }
}

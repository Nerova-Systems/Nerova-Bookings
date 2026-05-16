using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Schedules.Domain;

public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.MapStronglyTypedUuid<Schedule, ScheduleId>(schedule => schedule.Id);
        builder.MapStronglyTypedLongId<Schedule, TenantId>(schedule => schedule.TenantId);
        builder.MapStronglyTypedUuid<Schedule, UserId>(schedule => schedule.OwnerUserId);

        builder.Property(schedule => schedule.Name).HasMaxLength(120);
        builder.Property(schedule => schedule.TimeZone).HasMaxLength(100);
        builder.Property(schedule => schedule.AvailabilityWindows)
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value.ToArray(), JsonSerializerOptions),
                value => ImmutableArray.CreateRange(JsonSerializer.Deserialize<AvailabilityWindow[]>(value, JsonSerializerOptions)!)
            )
            .Metadata.SetValueComparer(new ValueComparer<ImmutableArray<AvailabilityWindow>>(
                    (left, right) => left.SequenceEqual(right),
                    windows => windows.Aggregate(0, (hash, window) => HashCode.Combine(hash, window.GetHashCode())),
                    windows => windows
                )
            );

        builder.HasIndex(schedule => new { schedule.TenantId, schedule.OwnerUserId, schedule.Name });
        builder.HasIndex(schedule => new { schedule.TenantId, schedule.OwnerUserId, schedule.IsDefault });
    }
}

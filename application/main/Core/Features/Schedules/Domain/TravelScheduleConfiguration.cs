using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Schedules.Domain;

public sealed class TravelScheduleConfiguration : IEntityTypeConfiguration<TravelSchedule>
{
    public void Configure(EntityTypeBuilder<TravelSchedule> builder)
    {
        builder.MapStronglyTypedUuid<TravelSchedule, TravelScheduleId>(travel => travel.Id);
        builder.MapStronglyTypedLongId<TravelSchedule, TenantId>(travel => travel.TenantId);
        builder.MapStronglyTypedUuid<TravelSchedule, UserId>(travel => travel.UserId);
        builder.MapStronglyTypedNullableId<TravelSchedule, ScheduleId, string>(travel => travel.ScheduleId);

        builder.Property(travel => travel.TimeZone).HasMaxLength(100);

        builder.HasIndex(travel => new { travel.TenantId, travel.UserId });
        builder.HasIndex(travel => new { travel.UserId, travel.StartDate, travel.EndDate });
    }
}

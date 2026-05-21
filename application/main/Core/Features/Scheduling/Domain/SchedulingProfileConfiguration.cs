using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Scheduling.Domain;

public sealed class SchedulingProfileConfiguration : IEntityTypeConfiguration<SchedulingProfile>
{
    public void Configure(EntityTypeBuilder<SchedulingProfile> builder)
    {
        builder.MapStronglyTypedUuid<SchedulingProfile, SchedulingProfileId>(profile => profile.Id);
        builder.MapStronglyTypedLongId<SchedulingProfile, TenantId>(profile => profile.TenantId);
        builder.MapStronglyTypedNullableLongId<SchedulingProfile, TenantId>(profile => profile.TeamId);
        builder.MapStronglyTypedUuid<SchedulingProfile, UserId>(profile => profile.OwnerUserId);

        builder.Property(profile => profile.Handle).HasMaxLength(60);
        builder.Property(profile => profile.DisplayName).HasMaxLength(120);
        builder.Property(profile => profile.AvatarUrl).HasMaxLength(500);

        builder.HasIndex(profile => profile.Handle).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(profile => new { profile.TenantId, profile.OwnerUserId }).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(profile => profile.TeamId);
    }
}

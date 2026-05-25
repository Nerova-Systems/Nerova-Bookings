using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Schedules.Domain;

public sealed class OutOfOfficeConfiguration : IEntityTypeConfiguration<OutOfOffice>
{
    public void Configure(EntityTypeBuilder<OutOfOffice> builder)
    {
        builder.MapStronglyTypedUuid<OutOfOffice, OutOfOfficeId>(ooo => ooo.Id);
        builder.MapStronglyTypedLongId<OutOfOffice, TenantId>(ooo => ooo.TenantId);
        builder.MapStronglyTypedUuid<OutOfOffice, UserId>(ooo => ooo.UserId);
        builder.MapStronglyTypedNullableId<OutOfOffice, UserId, string>(ooo => ooo.ToUserId);

        builder.Property(ooo => ooo.Reason).HasMaxLength(120);
        builder.Property(ooo => ooo.Notes).HasMaxLength(1000);

        builder.HasIndex(ooo => new { ooo.TenantId, ooo.UserId });
        builder.HasIndex(ooo => new { ooo.UserId, ooo.StartDate, ooo.EndDate });
    }
}

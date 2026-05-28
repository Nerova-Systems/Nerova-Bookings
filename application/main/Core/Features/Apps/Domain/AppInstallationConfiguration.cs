using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Apps.Domain;

public sealed class AppInstallationConfiguration : IEntityTypeConfiguration<AppInstallation>
{
    public void Configure(EntityTypeBuilder<AppInstallation> builder)
    {
        builder.MapStronglyTypedUuid<AppInstallation, AppInstallationId>(installation => installation.Id);
        builder.MapStronglyTypedLongId<AppInstallation, TenantId>(installation => installation.TenantId);
        builder.MapStronglyTypedId<AppInstallation, AppSlug, string>(installation => installation.AppSlug);
        builder.MapStronglyTypedUuid<AppInstallation, UserId>(installation => installation.InstalledByUserId);

        builder.HasIndex(installation => new { installation.TenantId, installation.AppSlug }).IsUnique();
    }
}

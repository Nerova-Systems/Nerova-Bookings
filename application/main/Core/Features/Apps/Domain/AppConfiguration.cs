using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.EntityFramework;

namespace Main.Features.Apps.Domain;

public sealed class AppConfiguration : IEntityTypeConfiguration<App>
{
    public void Configure(EntityTypeBuilder<App> builder)
    {
        builder.MapStronglyTypedId<App, AppSlug, string>(app => app.Id);

        builder.Property(app => app.Name).HasMaxLength(120);
        builder.Property(app => app.Description).HasMaxLength(2000);
        builder.Property(app => app.LogoUrl).HasMaxLength(500);
        builder.Property(app => app.Category).HasConversion<string>().HasMaxLength(40);

        builder.HasIndex(app => app.Category);
        builder.HasIndex(app => app.IsActive);
    }
}

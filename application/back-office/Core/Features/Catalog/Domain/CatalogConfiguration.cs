using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.EntityFramework;

namespace BackOffice.Features.Catalog.Domain;

public sealed class CatalogTenantConfiguration : IEntityTypeConfiguration<CatalogTenant>
{
    public void Configure(EntityTypeBuilder<CatalogTenant> builder)
    {
        builder.HasKey(t => t.Id);
        builder.MapStronglyTypedLongId(t => t.Id);
        builder.Property(t => t.Name).IsRequired();
        builder.Property(t => t.State).IsRequired();
        builder.Property(t => t.Plan).IsRequired();
        builder.HasIndex(t => t.DeletedAt);
    }
}

public sealed class CatalogUserConfiguration : IEntityTypeConfiguration<CatalogUser>
{
    public void Configure(EntityTypeBuilder<CatalogUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.MapStronglyTypedUuid(u => u.Id);
        builder.MapStronglyTypedLongId(u => u.TenantId);
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.Role).IsRequired();
        builder.HasIndex(u => u.Email);
        builder.HasIndex(u => u.TenantId);
        builder.HasIndex(u => u.DeletedAt);
    }
}

public sealed class ProcessedCatalogEventConfiguration : IEntityTypeConfiguration<ProcessedCatalogEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedCatalogEvent> builder)
    {
        builder.HasKey(e => e.Id);
    }
}

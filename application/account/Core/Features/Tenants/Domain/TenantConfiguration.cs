using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Tenants.Domain;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.MapStronglyTypedLongId<Tenant, TenantId>(t => t.Id);

        builder.OwnsOne(t => t.Logo, b => b.ToJson());

        // MapStronglyTypedNullableLongId is required because ParentTenantId is a nullable strongly-typed long.
        builder.MapStronglyTypedNullableLongId<Tenant, TenantId>(t => t.ParentTenantId);

        // HasDefaultValue is intentionally set here even though primitive property configuration is normally
        // avoided. It is needed so that EnsureCreated() in the SQLite test database creates the column with
        // DEFAULT 'Solo', allowing the 35+ existing raw SQL inserts that omit the kind column to continue
        // working without modification.
        builder.Property(t => t.Kind).HasDefaultValue(TenantKind.Solo);

        // Restrict deletion of an Organization that still has Team children — prevents orphaned teams.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(t => t.ParentTenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


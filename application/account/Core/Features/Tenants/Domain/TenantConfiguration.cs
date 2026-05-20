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

        // Boolean columns must carry HasDefaultValue(false) so that EnsureCreated() generates DEFAULT 0 in
        // the SQLite test schema. Without this, any raw-SQL INSERT that omits these columns (e.g. from
        // SqliteConnectionExtensions.Insert used by existing tests) hits a NOT NULL constraint failure.
        builder.Property(t => t.HideBranding).HasDefaultValue(false);
        builder.Property(t => t.HideTeamProfileLink).HasDefaultValue(false);
        builder.Property(t => t.IsPrivate).HasDefaultValue(false);
        builder.Property(t => t.HideBookATeamMember).HasDefaultValue(false);

        // Restrict deletion of an Organization that still has Team children — prevents orphaned teams.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(t => t.ParentTenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Organization slugs are globally unique among all organizations.
        // Partial index excludes null slugs and non-organization tenants.
        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasFilter("slug IS NOT NULL AND kind = 'Organization'")
            .HasDatabaseName("uix_tenants_slug_org");

        // Team slugs are unique within their parent organization.
        // Partial index excludes null slugs and non-team tenants.
        builder.HasIndex(t => new { t.Slug, t.ParentTenantId })
            .IsUnique()
            .HasFilter("slug IS NOT NULL AND kind = 'Team'")
            .HasDatabaseName("uix_tenants_slug_parent");
    }
}


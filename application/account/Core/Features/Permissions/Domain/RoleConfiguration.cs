using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Permissions.Domain;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.MapStronglyTypedUuid<Role, RoleId>(r => r.Id);

        // TenantId is nullable (null = system role, non-null = custom org role).
        builder.MapStronglyTypedNullableLongId<Role, TenantId>(r => r.TenantId);

        // Tenant relationship: Restrict to prevent accidental cascade-deletion of org-scoped
        // custom roles when a tenant is deleted (would orphan permission grants).
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // ─── Owned permissions collection ────────────────────────────────────────
        // Permission is a sealed record owned by Role, stored in a separate table.
        // EF Core resolves the backing field _permissions via naming convention
        // (_<camelCase of property name>) and is told to use it exclusively via
        // PropertyAccessMode.Field so it never tries to use the read-only property getter.

        builder.OwnsMany(r => r.Permissions, ownedBuilder =>
            {
                ownedBuilder.ToTable("role_permissions");

                // Shadow FK column; snake_case convention matches migration definition.
                ownedBuilder.WithOwner().HasForeignKey("role_id");

                // Auto-generated surrogate PK.  Without this, EF Core uses the sealed record's
                // structural equality to deduplicate owned entities in the change tracker: when multiple
                // roles share the same (Resource, Action) pair (e.g. Owner and Admin both have
                // EventType.Create), SaveChanges would only insert ONE row for the last role processed.
                // A surrogate key forces EF to treat every Permission instance as distinct.
                ownedBuilder.Property<long>("id").ValueGeneratedOnAdd();
                ownedBuilder.HasKey("id");

                // Enforce the uniqueness constraint at the DB level.
                ownedBuilder.HasIndex("role_id", nameof(Permission.Resource), nameof(Permission.Action))
                    .IsUnique()
                    .HasDatabaseName("uix_role_permissions_role_resource_action");
            }
        );

        builder.Navigation(r => r.Permissions)
            .HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ─── Uniqueness constraints ──────────────────────────────────────────────
        // System roles (tenant_id IS NULL) must be unique by name globally.
        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasFilter("tenant_id IS NULL")
            .HasDatabaseName("uix_roles_name_system");

        // Custom roles (tenant_id IS NOT NULL) must be unique per org.
        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique()
            .HasFilter("tenant_id IS NOT NULL")
            .HasDatabaseName("uix_roles_tenant_id_name");

        // Lookup: all custom roles for a given org.
        builder.HasIndex(r => r.TenantId)
            .HasFilter("tenant_id IS NOT NULL")
            .HasDatabaseName("ix_roles_tenant_id");
    }
}

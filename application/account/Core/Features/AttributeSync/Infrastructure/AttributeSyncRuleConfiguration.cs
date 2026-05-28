using Account.Features.Attributes.Domain;
using Account.Features.AttributeSync.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.AttributeSync.Infrastructure;

public sealed class AttributeSyncRuleConfiguration : IEntityTypeConfiguration<AttributeSyncRule>
{
    public void Configure(EntityTypeBuilder<AttributeSyncRule> builder)
    {
        builder.ToTable("attribute_sync_rules");

        builder.MapStronglyTypedUuid<AttributeSyncRule, AttributeSyncRuleId>(r => r.Id);
        builder.MapStronglyTypedLongId<AttributeSyncRule, TenantId>(r => r.TenantId);
        builder.MapStronglyTypedUuid<AttributeSyncRule, AttributeId>(r => r.AttributeId);

        builder.Property(r => r.ClaimPath).IsRequired();
        builder.Property(r => r.Mode).IsRequired();

        // Referential integrity: cascade-delete rules when their owning org is deleted.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lookup: all rules for a given org.
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_attribute_sync_rules_tenant_id");

        // Lookup: enabled rules per org (used on every SSO login).
        builder.HasIndex(r => new { r.TenantId, r.IsEnabled })
            .HasDatabaseName("ix_attribute_sync_rules_tenant_id_is_enabled");
    }
}

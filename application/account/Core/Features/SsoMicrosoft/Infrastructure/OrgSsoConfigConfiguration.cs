using Account.Features.Sso.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.SsoMicrosoft.Infrastructure;

public sealed class OrgSsoConfigConfiguration : IEntityTypeConfiguration<OrgSsoConfig>
{
    public void Configure(EntityTypeBuilder<OrgSsoConfig> builder)
    {
        builder.MapStronglyTypedUuid<OrgSsoConfig, OrgSsoConfigId>(c => c.Id);
        builder.MapStronglyTypedLongId<OrgSsoConfig, TenantId>(c => c.TenantId);

        builder.Property(c => c.Provider).IsRequired().HasConversion<string>();
        builder.Property(c => c.EncryptedProviderConfig).IsRequired();
        builder.Property(c => c.AllowedDomainsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(c => c.IsEnabled).HasDefaultValue(false);

        // Referential integrity: cascade-delete config when its org is removed.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one config per organization per provider.
        builder.HasIndex(c => new { c.TenantId, c.Provider })
            .IsUnique()
            .HasDatabaseName("uix_org_sso_configs_tenant_id_provider");
    }
}

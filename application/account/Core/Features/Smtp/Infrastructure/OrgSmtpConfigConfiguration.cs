using Account.Features.Smtp.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Smtp.Infrastructure;

public sealed class OrgSmtpConfigConfiguration : IEntityTypeConfiguration<OrgSmtpConfig>
{
    public void Configure(EntityTypeBuilder<OrgSmtpConfig> builder)
    {
        builder.MapStronglyTypedUuid<OrgSmtpConfig, OrgSmtpConfigId>(c => c.Id);
        builder.MapStronglyTypedLongId<OrgSmtpConfig, TenantId>(c => c.TenantId);

        builder.Property(c => c.Host).IsRequired();
        builder.Property(c => c.Username).IsRequired();
        builder.Property(c => c.EncryptedPassword).IsRequired();
        builder.Property(c => c.FromEmail).IsRequired();
        builder.Property(c => c.IsEnabled).HasDefaultValue(false);

        // Referential integrity: cascade-delete config when its org is removed.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one SMTP config per organization.
        builder.HasIndex(c => c.TenantId)
            .IsUnique()
            .HasDatabaseName("uix_org_smtp_configs_tenant_id");
    }
}

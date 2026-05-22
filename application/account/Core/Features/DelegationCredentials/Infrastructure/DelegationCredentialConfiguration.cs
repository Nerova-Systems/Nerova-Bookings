using Account.Features.DelegationCredentials.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.DelegationCredentials.Infrastructure;

public sealed class DelegationCredentialConfiguration : IEntityTypeConfiguration<DelegationCredential>
{
    public void Configure(EntityTypeBuilder<DelegationCredential> builder)
    {
        builder.MapStronglyTypedUuid<DelegationCredential, DelegationCredentialId>(c => c.Id);
        builder.MapStronglyTypedLongId<DelegationCredential, TenantId>(c => c.TenantId);
        builder.MapStronglyTypedUuid<DelegationCredential, UserId>(c => c.CreatedByUserId);

        builder.Property(c => c.Domain).IsRequired();
        builder.Property(c => c.EncryptedKeyBlob).IsRequired();
        builder.Property(c => c.Status).IsRequired().HasDefaultValue(DelegationCredentialStatus.Active);

        // Referential integrity: cascade-delete credentials when their org is removed.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one credential per (org, platform).
        builder.HasIndex(c => new { c.TenantId, c.Platform })
            .IsUnique()
            .HasDatabaseName("uix_delegation_credentials_tenant_id_platform");
    }
}

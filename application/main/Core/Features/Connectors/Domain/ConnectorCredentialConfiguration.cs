using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Connectors.Domain;

public sealed class ConnectorCredentialConfiguration : IEntityTypeConfiguration<ConnectorCredential>
{
    public void Configure(EntityTypeBuilder<ConnectorCredential> builder)
    {
        builder.HasKey(credential => credential.Id);
        builder.MapStronglyTypedLongId<ConnectorCredential, TenantId>(credential => credential.TenantId);
        builder.MapStronglyTypedUuid<ConnectorCredential, UserId>(credential => credential.OwnerUserId);

        builder.Property(credential => credential.Id).HasMaxLength(120);
        builder.Property(credential => credential.Integration).HasMaxLength(120);
        builder.Property(credential => credential.ExternalAccountId).HasMaxLength(500);
        builder.Property(credential => credential.AccountEmail).HasMaxLength(320);
        builder.Property(credential => credential.DisplayName).HasMaxLength(200);
        builder.Property(credential => credential.Status).HasMaxLength(80);
        builder.Property(credential => credential.SecretReference).HasMaxLength(500);
        builder.Property(credential => credential.CalendarsJson).HasColumnType("jsonb");

        builder.HasIndex(credential => new { credential.TenantId, credential.OwnerUserId, credential.Integration });
        builder.HasIndex(credential => new { credential.TenantId, credential.OwnerUserId, credential.Id });
        builder.HasIndex(credential => new { credential.TenantId, credential.OwnerUserId, credential.Integration, credential.ExternalAccountId })
            .IsUnique();
    }
}

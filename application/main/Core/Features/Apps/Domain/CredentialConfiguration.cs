using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Apps.Domain;

public sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.MapStronglyTypedUuid<Credential, CredentialId>(credential => credential.Id);
        builder.MapStronglyTypedLongId<Credential, TenantId>(credential => credential.TenantId);
        builder.MapStronglyTypedUuid<Credential, UserId>(credential => credential.UserId);
        builder.MapStronglyTypedId<Credential, AppSlug, string>(credential => credential.AppSlug);

        builder.Property(credential => credential.EncryptedKey).HasColumnType("text");

        builder.HasIndex(credential => new { credential.TenantId, credential.UserId, credential.AppSlug }).IsUnique();
        builder.HasIndex(credential => credential.AppSlug);
    }
}

using Account.Features.ApiKeys.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.ApiKeys.Infrastructure;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.MapStronglyTypedUuid<ApiKey, ApiKeyId>(k => k.Id);
        builder.MapStronglyTypedLongId<ApiKey, TenantId>(k => k.TenantId);
        builder.MapStronglyTypedUuid<ApiKey, UserId>(k => k.CreatedByUserId);

        builder.Property(k => k.Name).IsRequired().HasMaxLength(100);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(64);
        builder.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(20);

        // Referential integrity: cascade-delete keys when their owning tenant is removed.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(k => k.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hash must be globally unique — it is the lookup key during authentication.
        builder.HasIndex(k => k.KeyHash)
            .IsUnique()
            .HasDatabaseName("uix_api_keys_key_hash");
    }
}

using JetBrains.Annotations;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Main.Features.Connectors.Domain;

public sealed class ConnectorTokenSecret : AggregateRoot<string>, ITenantScopedEntity
{
    [UsedImplicitly]
    private ConnectorTokenSecret() : base(string.Empty)
    {
        CredentialId = string.Empty;
        ProtectedPayload = string.Empty;
    }

    private ConnectorTokenSecret(TenantId tenantId, string id, string credentialId, string protectedPayload) : base(id)
    {
        TenantId = tenantId;
        CredentialId = credentialId.Trim();
        ProtectedPayload = protectedPayload;
    }

    public TenantId TenantId { get; } = new(0);

    public string CredentialId { get; }

    public string ProtectedPayload { get; private set; }

    public static ConnectorTokenSecret Create(TenantId tenantId, string id, string credentialId, string protectedPayload)
    {
        return new ConnectorTokenSecret(tenantId, id, credentialId, protectedPayload);
    }

    public void UpdateProtectedPayload(string protectedPayload)
    {
        ProtectedPayload = protectedPayload;
    }
}

public sealed class ConnectorTokenSecretConfiguration : IEntityTypeConfiguration<ConnectorTokenSecret>
{
    public void Configure(EntityTypeBuilder<ConnectorTokenSecret> builder)
    {
        builder.HasKey(secret => secret.Id);
        builder.MapStronglyTypedLongId<ConnectorTokenSecret, TenantId>(secret => secret.TenantId);
        builder.Property(secret => secret.Id).HasMaxLength(160);
        builder.Property(secret => secret.CredentialId).HasMaxLength(120);
        builder.Property(secret => secret.ProtectedPayload).HasColumnType("text");

        builder.HasIndex(secret => new { secret.TenantId, secret.Id });
        builder.HasIndex(secret => new { secret.TenantId, secret.CredentialId }).IsUnique();
        builder.HasOne<ConnectorCredential>()
            .WithMany()
            .HasForeignKey(secret => secret.CredentialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public interface IConnectorTokenSecretRepository : ICrudRepository<ConnectorTokenSecret, string>
{
    Task<ConnectorTokenSecret?> GetForTenantAsync(TenantId tenantId, string id, CancellationToken cancellationToken);

    Task<ConnectorTokenSecret?> GetForCredentialAsync(TenantId tenantId, string credentialId, CancellationToken cancellationToken);
}

public sealed class ConnectorTokenSecretRepository(MainDbContext mainDbContext)
    : RepositoryBase<ConnectorTokenSecret, string>(mainDbContext), IConnectorTokenSecretRepository
{
    public async Task<ConnectorTokenSecret?> GetForTenantAsync(TenantId tenantId, string id, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(secret => secret.TenantId == tenantId)
            .Where(secret => secret.Id == id.Trim())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ConnectorTokenSecret?> GetForCredentialAsync(TenantId tenantId, string credentialId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(secret => secret.TenantId == tenantId)
            .Where(secret => secret.CredentialId == credentialId.Trim())
            .SingleOrDefaultAsync(cancellationToken);
    }
}

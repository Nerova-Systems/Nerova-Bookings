using Main.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Connectors.Domain;

public interface IConnectorCredentialRepository : ICrudRepository<ConnectorCredential, string>
{
    Task<ConnectorCredential[]> GetCoreForOwnerAsync(TenantId tenantId, UserId ownerUserId, CancellationToken cancellationToken);

    Task<ConnectorCredential?> GetOwnedAsync(TenantId tenantId, UserId ownerUserId, string id, CancellationToken cancellationToken);

    Task<ConnectorCredential?> GetOwnedByExternalAccountAsync(
        TenantId tenantId,
        UserId ownerUserId,
        string integration,
        string externalAccountId,
        CancellationToken cancellationToken
    );

    Task<ConnectorCredential[]> GetForTenantByIdsAsync(TenantId tenantId, string[] ids, CancellationToken cancellationToken);

    Task RemoveTestFixturesForOwnerAsync(TenantId tenantId, UserId ownerUserId, string[] retainedCredentialIds, CancellationToken cancellationToken);
}

public sealed class ConnectorCredentialRepository(MainDbContext mainDbContext)
    : RepositoryBase<ConnectorCredential, string>(mainDbContext), IConnectorCredentialRepository
{
    public async Task<ConnectorCredential[]> GetCoreForOwnerAsync(TenantId tenantId, UserId ownerUserId, CancellationToken cancellationToken)
    {
        var credentials = await DbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => credential.OwnerUserId == ownerUserId)
            .ToArrayAsync(cancellationToken);

        return credentials
            .Where(credential =>
                CoreConnectorConstants.IsCoreCalendar(credential.Integration) ||
                credential.Integration.Equals(CoreConnectorConstants.ZoomVideo, StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(credential => ConnectorSortOrder(credential.Integration))
            .ThenBy(credential => credential.DisplayName)
            .ThenBy(credential => credential.Id)
            .ToArray();
    }

    public async Task<ConnectorCredential?> GetOwnedAsync(TenantId tenantId, UserId ownerUserId, string id, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => credential.OwnerUserId == ownerUserId)
            .Where(credential => credential.Id == id.Trim())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ConnectorCredential?> GetOwnedByExternalAccountAsync(
        TenantId tenantId,
        UserId ownerUserId,
        string integration,
        string externalAccountId,
        CancellationToken cancellationToken
    )
    {
        var normalizedIntegration = integration.Trim();
        var normalizedExternalAccountId = externalAccountId.Trim();
        return await DbSet
            .IgnoreQueryFilters()
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => credential.OwnerUserId == ownerUserId)
            .Where(credential => credential.Integration == normalizedIntegration)
            .Where(credential => credential.ExternalAccountId == normalizedExternalAccountId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ConnectorCredential[]> GetForTenantByIdsAsync(TenantId tenantId, string[] ids, CancellationToken cancellationToken)
    {
        var normalizedIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedIds.Length == 0) return [];

        var credentials = await DbSet
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(credential => credential.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);

        return credentials
            .Where(credential => Array.IndexOf(normalizedIds, credential.Id) >= 0)
            .ToArray();
    }

    public async Task RemoveTestFixturesForOwnerAsync(TenantId tenantId, UserId ownerUserId, string[] retainedCredentialIds, CancellationToken cancellationToken)
    {
        var credentials = await DbSet
            .IgnoreQueryFilters()
            .Where(credential => credential.TenantId == tenantId)
            .Where(credential => credential.OwnerUserId == ownerUserId)
            .Where(credential =>
                credential.Id.StartsWith("fake-busy:") ||
                credential.Id.StartsWith("e2e-office365-calendar:") ||
                credential.Id.StartsWith("e2e-zoom-video:")
            )
            .ToArrayAsync(cancellationToken);

        RemoveRange(credentials.Where(credential => Array.IndexOf(retainedCredentialIds, credential.Id) < 0).ToArray());
    }

    private static int ConnectorSortOrder(string integration)
    {
        return integration.ToLowerInvariant() switch
        {
            CoreConnectorConstants.GoogleCalendar => 0,
            CoreConnectorConstants.Office365Calendar => 1,
            CoreConnectorConstants.ZoomVideo => 2,
            _ => 100
        };
    }
}

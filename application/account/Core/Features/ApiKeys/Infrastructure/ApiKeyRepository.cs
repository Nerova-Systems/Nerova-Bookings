using Account.Database;
using Account.Features.ApiKeys.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.ApiKeys.Infrastructure;

/// <summary>
///     Repository for <see cref="ApiKey" /> aggregates.
/// </summary>
public sealed class ApiKeyRepository(AccountDbContext context)
    : RepositoryBase<ApiKey, ApiKeyId>(context), IApiKeyRepository
{
    public Task<ApiKey?> GetByHashAsync(string hash, CancellationToken cancellationToken)
    {
        // Tenant filter is bypassed because API key authentication is cross-tenant by design:
        // we receive only the raw token and must locate the key before knowing which tenant it belongs to.
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKey>> GetByUserAsync(TenantId userTenantId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(k => k.TenantId == userTenantId && k.Scope == ApiKeyScope.User)
            .OrderByDescending(k => k.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApiKey>> GetByOrgAsync(TenantId orgTenantId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(k => k.TenantId == orgTenantId && k.Scope == ApiKeyScope.Organization)
            .OrderByDescending(k => k.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<ApiKey?> GetByIdUnfilteredAsync(ApiKeyId id, CancellationToken cancellationToken)
    {
        // Required for org-scope revocation where the key's tenant differs from the caller's solo tenant.
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(k => k.Id == id, cancellationToken);
    }
}

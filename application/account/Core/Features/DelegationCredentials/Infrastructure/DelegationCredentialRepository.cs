using Account.Database;
using Account.Features.DelegationCredentials.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.DelegationCredentials.Infrastructure;

/// <summary>
///     Repository for <see cref="DelegationCredential" /> aggregates.
/// </summary>
public sealed class DelegationCredentialRepository(AccountDbContext context)
    : RepositoryBase<DelegationCredential, DelegationCredentialId>(context), IDelegationCredentialRepository
{
    public Task<DelegationCredential?> GetByOrgAndPlatformAsync(
        TenantId orgId,
        WorkspacePlatform platform,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters(Tenant) because the explicit predicate already scopes this query
        // correctly. The tenant filter would add a redundant AND on executionContext.TenantId,
        // which fails in background workers or test contexts without an HTTP request.
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(c => c.TenantId == orgId && c.Platform == platform, cancellationToken);
    }

    public Task<DelegationCredential[]> GetAllByOrgIdAsync(TenantId orgId, CancellationToken cancellationToken)
    {
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(c => c.TenantId == orgId)
            .ToArrayAsync(cancellationToken);
    }
}

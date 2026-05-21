using Account.Database;
using Account.Features.Sso.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.SsoMicrosoft.Infrastructure;

/// <summary>
///     Repository for <see cref="OrgSsoConfig" /> aggregates.
///     Ignores the tenant query filter so any org's config can be loaded regardless of the current actor's tenant.
/// </summary>
public sealed class OrgSsoConfigRepository(AccountDbContext context)
    : RepositoryBase<OrgSsoConfig, OrgSsoConfigId>(context), IOrgSsoConfigRepository
{
    public Task<OrgSsoConfig?> GetByOrgAndProviderAsync(TenantId orgId, SsoProvider provider, CancellationToken cancellationToken)
    {
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(c => c.TenantId == orgId && c.Provider == provider, cancellationToken);
    }
}

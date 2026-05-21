using Account.Database;
using Account.Features.Smtp.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.Smtp.Infrastructure;

/// <summary>
///     Repository for <see cref="OrgSmtpConfig" /> aggregates.
/// </summary>
public sealed class OrgSmtpConfigRepository(AccountDbContext context)
    : RepositoryBase<OrgSmtpConfig, OrgSmtpConfigId>(context), IOrgSmtpConfigRepository
{
    public Task<OrgSmtpConfig?> GetByOrgIdAsync(TenantId orgId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters(Tenant) because the explicit c.TenantId == orgId predicate already
        // scopes this query correctly. The tenant filter would add a redundant AND on
        // executionContext.TenantId, which causes problems when the filter's TenantId is null
        // (e.g., background workers or test contexts without an HTTP request).
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(c => c.TenantId == orgId, cancellationToken);
    }
}

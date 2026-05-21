using Account.Database;
using Account.Features.AttributeSync.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.AttributeSync.Infrastructure;

/// <summary>
///     Repository for <see cref="AttributeSyncRule" /> aggregates.
/// </summary>
/// <remarks>
///     All unfiltered methods bypass the global tenant query filter because
///     <see cref="AttributeSyncRule.TenantId" /> is an org tenant ID, which differs from the
///     solo tenant ID held in the execution context.
/// </remarks>
public sealed class AttributeSyncRuleRepository(AccountDbContext context)
    : RepositoryBase<AttributeSyncRule, AttributeSyncRuleId>(context), IAttributeSyncRuleRepository
{
    public async Task<IReadOnlyList<AttributeSyncRule>> GetByOrgUnfilteredAsync(
        TenantId orgTenantId,
        CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(r => r.TenantId == orgTenantId)
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeSyncRule>> GetEnabledByOrgUnfilteredAsync(
        TenantId orgTenantId,
        CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(r => r.TenantId == orgTenantId && r.IsEnabled)
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<AttributeSyncRule?> GetByIdUnfilteredAsync(
        AttributeSyncRuleId id,
        CancellationToken cancellationToken)
    {
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }
}

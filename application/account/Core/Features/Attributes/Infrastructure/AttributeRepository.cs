using Account.Database;
using Account.Features.Attributes.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;
using SharedKernel.Persistence;

namespace Account.Features.Attributes.Infrastructure;

/// <summary>
///     Repository for <see cref="Domain.Attribute" /> aggregates.
/// </summary>
public sealed class AttributeRepository(AccountDbContext context)
    : RepositoryBase<Domain.Attribute, AttributeId>(context), IAttributeRepository
{
    /// <remarks>
    ///     Bypasses the global tenant query filter because <see cref="Domain.Attribute.TenantId" />
    ///     is an org tenant ID, which differs from the solo tenant ID in the execution context.
    /// </remarks>
    public async Task<IReadOnlyList<Domain.Attribute>> GetByOrgUnfilteredAsync(
        TenantId orgTenantId,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters(Tenant) because Attribute.TenantId is an org tenant ID, which
        // differs from executionContext.TenantId (the user's solo tenant). All attribute
        // repository methods bypass the filter and scope manually via the predicate.
        return await DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(a => a.TenantId == orgTenantId)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    /// <remarks>
    ///     Bypasses the global tenant query filter. See <see cref="GetByOrgUnfilteredAsync" /> for rationale.
    /// </remarks>
    public Task<Domain.Attribute?> GetByIdUnfilteredAsync(AttributeId id, CancellationToken cancellationToken)
    {
        // OwnsMany collections are NOT loaded via FindAsync; use a LINQ query with Include to
        // materialise the Options collection alongside the aggregate.
        // AsTracking() is required because SharedKernelDbContext sets NoTracking globally.
        // Without it RepositoryBase.Update() falls through to DbSet.Update(aggregate), which
        // marks the entire graph—including any newly-added owned entities—as Modified.
        // A new AttributeOption has no DB row yet, so its UPDATE would affect 0 rows and
        // throw DbUpdateConcurrencyException.
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AsTracking()
            .Include(a => a.Options)
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <remarks>
    ///     Bypasses the global tenant query filter. See <see cref="GetByOrgUnfilteredAsync" /> for rationale.
    /// </remarks>
    public Task<bool> SlugExistsUnfilteredAsync(TenantId orgTenantId, string slug, CancellationToken cancellationToken)
    {
        return DbSet
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .AnyAsync(a => a.TenantId == orgTenantId && a.Slug == slug, cancellationToken);
    }
}

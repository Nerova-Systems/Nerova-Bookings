using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Account.Features.Permissions.Domain;

public interface IRoleRepository : ICrudRepository<Role, RoleId>
{
    /// <summary>
    ///     Loads the role with its owned <see cref="Role.Permissions" /> collection eagerly populated.
    ///     EF Core's <c>FindAsync</c> (used by the base <c>GetByIdAsync</c>) does not auto-include
    ///     owned collections, so command-side handlers that mutate or delete the role MUST call this
    ///     overload to ensure the children are tracked and the shadow surrogate key is known.
    /// </summary>
    Task<Role?> GetByIdWithPermissionsAsync(RoleId id, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all three system roles (Owner, Admin, Member) with their permission sets loaded.
    /// </summary>
    Task<Role[]> GetSystemRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all custom roles scoped to the given tenant, with their permission sets loaded.
    /// </summary>
    Task<Role[]> GetCustomRolesForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns the custom role with the given name in the given tenant, or <see langword="null" />
    ///     if no match is found.
    /// </summary>
    Task<Role?> GetByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken);
}

public sealed class RoleRepository(AccountDbContext accountDbContext)
    : RepositoryBase<Role, RoleId>(accountDbContext), IRoleRepository
{
    /// <summary>
    ///     Overrides the base <c>FindAsync</c> implementation to include the owned permissions
    ///     collection. EF Core's <c>FindAsync</c> does not automatically eager-load OwnsMany
    ///     navigations, so we use a keyed single-entity query instead.
    /// </summary>
    public Task<Role?> GetByIdWithPermissionsAsync(RoleId id, CancellationToken cancellationToken)
    {
        // AsTracking() is required because SharedKernelDbContext sets NoTracking globally.
        // Without it, Included owned permissions are detached → EF cannot preserve the
        // shadow surrogate 'id' on Update/Delete and SaveChanges throws
        // "shadow key property 'Permission.id' is unknown". Mirrors AttributeRepository.GetByIdUnfilteredAsync.
        return DbSet
            .AsTracking()
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<Role[]> GetSystemRolesAsync(CancellationToken cancellationToken)
    {
        return DbSet
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == null)
            .ToArrayAsync(cancellationToken);
    }

    public Task<Role[]> GetCustomRolesForTenantAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);
    }

    public Task<Role?> GetByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken)
    {
        return DbSet
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Name == name, cancellationToken);
    }
}

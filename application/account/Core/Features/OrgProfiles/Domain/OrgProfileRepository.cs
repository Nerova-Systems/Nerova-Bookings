using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Account.Features.OrgProfiles.Domain;

public interface IOrgProfileRepository : ICrudRepository<OrgProfile, OrgProfileId>
{
    /// <summary>
    ///     Returns the org profile for the given user in the given organization, or
    ///     <see langword="null" /> if no profile exists. Used for "what is my display identity
    ///     in this org?" lookups.
    /// </summary>
    Task<OrgProfile?> GetByUserAndOrgAsync(UserId userId, TenantId orgTenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns the org profile with the given username (slug) in the given organization, or
    ///     <see langword="null" /> if no match is found. Used for org-subdomain URL routing
    ///     (e.g., <c>https://acme.nerova.io/john-doe</c>).
    /// </summary>
    Task<OrgProfile?> GetByOrgAndUsernameAsync(TenantId orgTenantId, string username, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all org profiles for the given user across all organizations.
    ///     This query crosses tenant boundaries — <see cref="OrgProfile" /> is intentionally NOT
    ///     <c>ITenantScopedEntity</c> so no filter suppression is needed here.
    /// </summary>
    Task<OrgProfile[]> GetByUserAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all org profiles in the given organization. Used for the org settings UI member
    ///     listing.
    /// </summary>
    Task<OrgProfile[]> GetMembersOfOrgAsync(TenantId orgTenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns <see langword="true" /> if <paramref name="username" /> is available within
    ///     <paramref name="orgTenantId" />.
    ///     <para>
    ///         When <paramref name="excludeId" /> is provided, the profile with that ID is excluded
    ///         from the check. Use this when updating an existing profile to prevent it from
    ///         conflicting with itself.
    ///     </para>
    /// </summary>
    /// <param name="orgTenantId">The organization to check uniqueness within.</param>
    /// <param name="username">The candidate username to test.</param>
    /// <param name="excludeId">
    ///     Optional ID to exclude from the check (pass the current profile's ID when updating).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsUsernameAvailableAsync(TenantId orgTenantId, string username, OrgProfileId? excludeId, CancellationToken cancellationToken);
}

public sealed class OrgProfileRepository(AccountDbContext accountDbContext)
    : RepositoryBase<OrgProfile, OrgProfileId>(accountDbContext), IOrgProfileRepository
{
    public Task<OrgProfile?> GetByUserAndOrgAsync(UserId userId, TenantId orgTenantId, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(p => p.UserId == userId && p.OrgTenantId == orgTenantId, cancellationToken);
    }

    public Task<OrgProfile?> GetByOrgAndUsernameAsync(TenantId orgTenantId, string username, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(p => p.OrgTenantId == orgTenantId && p.Username == username, cancellationToken);
    }

    public Task<OrgProfile[]> GetByUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        return DbSet.Where(p => p.UserId == userId).ToArrayAsync(cancellationToken);
    }

    public Task<OrgProfile[]> GetMembersOfOrgAsync(TenantId orgTenantId, CancellationToken cancellationToken)
    {
        return DbSet.Where(p => p.OrgTenantId == orgTenantId).ToArrayAsync(cancellationToken);
    }

    public async Task<bool> IsUsernameAvailableAsync(TenantId orgTenantId, string username, OrgProfileId? excludeId, CancellationToken cancellationToken)
    {
        var query = DbSet.Where(p => p.OrgTenantId == orgTenantId && p.Username == username);

        if (excludeId is not null)
        {
            query = query.Where(p => p.Id != excludeId);
        }

        return !await query.AnyAsync(cancellationToken);
    }
}

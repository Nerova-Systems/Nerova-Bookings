using Account.Database;
using Account.Features.Permissions.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Account.Features.Memberships.Domain;

public interface IMembershipRepository : ICrudRepository<Membership, MembershipId>
{
    /// <summary>
    ///     Returns the membership for the given user in the given team/org, or <see langword="null" />
    ///     if the user is not a member.
    /// </summary>
    Task<Membership?> GetByUserAndTenantAsync(UserId userId, TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns the pending membership associated with the given invite token, or
    ///     <see langword="null" /> if no match is found.
    /// </summary>
    Task<Membership?> GetByInviteTokenAsync(string inviteToken, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all memberships for the given team/org.
    /// </summary>
    /// <param name="tenantId">The team or organization to query.</param>
    /// <param name="includePending">
    ///     When <see langword="true" /> (default), includes pending (not-yet-accepted) memberships.
    ///     When <see langword="false" />, returns only accepted members.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Membership[]> GetMembersOfTenantAsync(TenantId tenantId, bool includePending, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all memberships for the given user across all teams and organizations.
    ///     This query crosses tenant boundaries — <see cref="Membership" /> is intentionally NOT
    ///     <c>ITenantScopedEntity</c> so no filter suppression is needed here.
    /// </summary>
    Task<Membership[]> GetMembershipsOfUserAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns the number of <see cref="MembershipRole.Owner" /> memberships in the given
    ///     team/org. Used by the command layer to enforce the "last owner cannot be removed" invariant
    ///     before calling <see cref="Membership.ChangeRole" />.
    /// </summary>
    Task<int> CountOwnersAsync(TenantId tenantId, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns all memberships whose <see cref="Membership.CustomRoleId" /> matches the supplied
    ///     role. Used by <c>DeleteRoleCommand</c> to unassign the custom role from every member before
    ///     the role itself is removed.
    /// </summary>
    Task<Membership[]> GetByCustomRoleIdAsync(RoleId roleId, CancellationToken cancellationToken);
}

public sealed class MembershipRepository(AccountDbContext accountDbContext)
    : RepositoryBase<Membership, MembershipId>(accountDbContext), IMembershipRepository
{
    public Task<Membership?> GetByUserAndTenantAsync(UserId userId, TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, cancellationToken);
    }

    public Task<Membership?> GetByInviteTokenAsync(string inviteToken, CancellationToken cancellationToken)
    {
        return DbSet.SingleOrDefaultAsync(m => m.InviteToken == inviteToken, cancellationToken);
    }

    public async Task<Membership[]> GetMembersOfTenantAsync(TenantId tenantId, bool includePending, CancellationToken cancellationToken)
    {
        var query = DbSet.Where(m => m.TenantId == tenantId);

        if (!includePending)
        {
            query = query.Where(m => m.Accepted);
        }

        return await query.ToArrayAsync(cancellationToken);
    }

    public Task<Membership[]> GetMembershipsOfUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        return DbSet.Where(m => m.UserId == userId).ToArrayAsync(cancellationToken);
    }

    public Task<int> CountOwnersAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return DbSet.CountAsync(m => m.TenantId == tenantId && m.Role == MembershipRole.Owner, cancellationToken);
    }

    public Task<Membership[]> GetByCustomRoleIdAsync(RoleId roleId, CancellationToken cancellationToken)
    {
        return DbSet.Where(m => m.CustomRoleId == roleId).ToArrayAsync(cancellationToken);
    }
}

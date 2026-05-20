using System.Diagnostics;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using SharedKernel.Domain;

namespace Account.Features.Permissions.Services;

/// <summary>
///     Evaluates whether a given user holds a specific permission within a tenant.
///     <para>
///         Two paths exist depending on the tenant kind:<br />
///         • <see cref="TenantKind.Solo" />: permissions are derived from <see cref="User.Role" /> via the static
///         system-role permission sets — no <c>Membership</c> row is involved.<br />
///         • <see cref="TenantKind.Team" /> / <see cref="TenantKind.Organization" />: permissions are resolved from
///         the accepted <see cref="Membership" />. If the membership carries a
///         <see cref="Membership.CustomRoleId" />, the custom <see cref="Role" />'s permissions are used; otherwise
///         the static system-role set for the member's <see cref="MembershipRole" /> is used.
///     </para>
///     <para>
///         Results are cached per <c>(userId, tenantId)</c> pair for the lifetime of the HTTP request (the service is
///         registered as <c>Scoped</c>). This avoids repeated DB round-trips when a single request calls
///         <see cref="HasPermissionAsync" /> multiple times.
///     </para>
/// </summary>
public interface IPermissionCheckService
{
    /// <summary>Returns <see langword="true" /> if <paramref name="userId" /> holds <paramref name="permission" /> within <paramref name="tenantId" />.</summary>
    Task<bool> HasPermissionAsync(UserId userId, TenantId tenantId, Permission permission, CancellationToken cancellationToken);
}

public sealed class PermissionCheckService(
    IMembershipRepository membershipRepository,
    IRoleRepository roleRepository,
    ITenantRepository tenantRepository,
    IUserRepository userRepository) : IPermissionCheckService
{
    private readonly Dictionary<(UserId, TenantId), IReadOnlySet<Permission>> _cache = [];

    public async Task<bool> HasPermissionAsync(UserId userId, TenantId tenantId, Permission permission, CancellationToken cancellationToken)
    {
        var permissions = await ResolvePermissionsAsync(userId, tenantId, cancellationToken);
        return permissions.Contains(permission);
    }

    private async Task<IReadOnlySet<Permission>> ResolvePermissionsAsync(UserId userId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var key = (userId, tenantId);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var tenant = await tenantRepository.GetByIdUnfilteredAsync(tenantId, cancellationToken);

        IReadOnlySet<Permission> permissions;

        if (tenant is null)
        {
            permissions = new HashSet<Permission>();
        }
        else if (tenant.Kind == TenantKind.Solo)
        {
            var user = await userRepository.GetByIdUnfilteredAsync(userId, cancellationToken);
            permissions = user is null
                ? new HashSet<Permission>()
                : SystemRoles.GetPermissionsForRole(MapUserRole(user.Role)).ToHashSet();
        }
        else
        {
            var membership = await membershipRepository.GetByUserAndTenantAsync(userId, tenantId, cancellationToken);

            if (membership is null || !membership.Accepted)
            {
                permissions = new HashSet<Permission>();
            }
            else if (membership.CustomRoleId is not null)
            {
                var customRole = await roleRepository.GetByIdAsync(membership.CustomRoleId, cancellationToken);
                permissions = customRole is not null ? customRole.Permissions.ToHashSet() : new HashSet<Permission>();
            }
            else
            {
                permissions = SystemRoles.GetPermissionsForRole(membership.Role).ToHashSet();
            }
        }

        _cache[key] = permissions;
        return permissions;
    }

    private static MembershipRole MapUserRole(UserRole role) => role switch
    {
        UserRole.Owner => MembershipRole.Owner,
        UserRole.Admin => MembershipRole.Admin,
        UserRole.Member => MembershipRole.Member,
        _ => throw new UnreachableException($"Unknown UserRole: {role}.")
    };
}

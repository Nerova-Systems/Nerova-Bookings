using Main.Features.Permissions.Domain;
using SharedKernel.Authentication;
using SharedKernel.Domain;

namespace Main.Features.Permissions.Services;

/// <summary>
///     Evaluates whether a <see cref="UserInfo" /> holds a specific scheduling-domain permission
///     within a tenant. The main SCS does not have access to the Account SCS's
///     <c>Membership</c> / <c>Role</c> tables, so permissions are resolved purely from the
///     <see cref="UserInfo.Role" /> string propagated via the JWT.
///     <para>
///         See <see cref="SystemRoles" /> for the role→permission mapping and the deferred custom-role
///         note.
///     </para>
/// </summary>
public interface IPermissionCheckService
{
    /// <summary>
    ///     Returns <see langword="true" /> if <paramref name="userInfo" /> holds
    ///     <paramref name="permission" /> within <paramref name="tenantId" />.
    /// </summary>
    bool HasPermission(UserInfo userInfo, TenantId tenantId, Permission permission);
}

public sealed class PermissionCheckService : IPermissionCheckService
{
    public bool HasPermission(UserInfo userInfo, TenantId tenantId, Permission permission)
    {
        // The tenant scope is honoured by the calling pipeline behaviour (which selects the right
        // TenantId — current / team / org). Here we just check membership of the static role set.
        // A future change to support per-tenant custom roles will read additional state here.
        _ = tenantId;
        return SystemRoles.GetPermissionsForRole(userInfo.Role).Contains(permission);
    }
}

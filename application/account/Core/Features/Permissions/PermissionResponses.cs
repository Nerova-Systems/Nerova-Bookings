using Account.Features.Permissions.Domain;
using JetBrains.Annotations;

namespace Account.Features.Permissions;

/// <summary>Serialisable representation of a single <see cref="Permission" /> in API responses.</summary>
[PublicAPI]
public sealed record PermissionResponse(
    PermissionResource Resource,
    PermissionAction Action,
    string Key
);

/// <summary>Serialisable representation of a <see cref="Role" /> as returned by the API.</summary>
[PublicAPI]
public sealed record RoleResponse(
    RoleId Id,
    string Name,
    string? Description,
    bool IsSystem,
    int MemberCount,
    PermissionResponse[] Permissions
);

/// <summary>
///     Catalog of every <see cref="Permission" /> grouped by <see cref="PermissionResource" />.
///     Returned by <c>GET /api/account/permissions</c> to drive the PBAC admin UI.
/// </summary>
[PublicAPI]
public sealed record PermissionGroupResponse(
    PermissionResource Resource,
    PermissionResponse[] Permissions
);

/// <summary>Mapping helpers shared across PBAC command and query handlers.</summary>
public static class PermissionMappings
{
    public static PermissionResponse ToResponse(this Permission permission)
    {
        return new PermissionResponse(permission.Resource, permission.Action, permission.ToString());
    }

    public static RoleResponse ToResponse(this Role role, int memberCount)
    {
        return new RoleResponse(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystem,
            memberCount,
            role.Permissions.Select(p => p.ToResponse()).ToArray()
        );
    }
}

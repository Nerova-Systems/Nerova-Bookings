namespace Main.Features.Permissions.Domain;

/// <summary>
///     Static permission sets that map a system role string (the value of
///     <see cref="SharedKernel.Authentication.UserInfo.Role" /> propagated via the JWT) to the
///     concrete set of scheduling-domain permissions that role grants in the main SCS.
///     <para>
///         Membership and role assignment are owned by the Account SCS; main does not query the
///         Account database directly (the projects do not reference each other). Instead, main
///         relies on the role string already present in <see cref="SharedKernel.Authentication.UserInfo" />.
///     </para>
///     <para>
///         <b>Deferred:</b> custom PBAC roles defined via <c>Membership.CustomRoleId</c> in the Account
///         SCS are not yet observable from main. Plumbing the custom-role permission set across the
///         SCS boundary (e.g., via additional JWT claims or an account→main contract) is a follow-up
///         task. Until then, custom-role members are evaluated against their system <c>Role</c>.
///     </para>
/// </summary>
public static class SystemRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";

    /// <summary>Owners receive every permission defined in the main scheduling catalogue.</summary>
    public static readonly IReadOnlySet<Permission> OwnerPermissions = BuildAll();

    /// <summary>Admins receive every permission (same as Owner for the scheduling catalogue).</summary>
    public static readonly IReadOnlySet<Permission> AdminPermissions = BuildAll();

    /// <summary>
    ///     Members receive day-to-day permissions: read/create/update across all three resources, plus
    ///     the booking-update sub-actions (cancel, reschedule) and event-type duplication.
    ///     Members cannot reassign bookings, run reports, or delete resources.
    /// </summary>
    public static readonly IReadOnlySet<Permission> MemberPermissions = new HashSet<Permission>
    {
        new(PermissionResource.Booking, PermissionAction.Read),
        new(PermissionResource.Booking, PermissionAction.Create),
        new(PermissionResource.Booking, PermissionAction.Update),
        new(PermissionResource.Booking, PermissionAction.Cancel),
        new(PermissionResource.Booking, PermissionAction.Reschedule),
        new(PermissionResource.EventType, PermissionAction.Read),
        new(PermissionResource.EventType, PermissionAction.Create),
        new(PermissionResource.EventType, PermissionAction.Update),
        new(PermissionResource.EventType, PermissionAction.Duplicate),
        new(PermissionResource.Schedule, PermissionAction.Read),
        new(PermissionResource.Schedule, PermissionAction.Create),
        new(PermissionResource.Schedule, PermissionAction.Update)
    };

    /// <summary>
    ///     Resolves the permission set for the given role string. Unknown role strings (including
    ///     <see langword="null" />) yield the empty set — i.e., no permissions, fail-closed.
    /// </summary>
    public static IReadOnlySet<Permission> GetPermissionsForRole(string? role)
    {
        return role switch
        {
            Owner => OwnerPermissions,
            Admin => AdminPermissions,
            Member => MemberPermissions,
            _ => FrozenEmpty
        };
    }

    private static readonly IReadOnlySet<Permission> FrozenEmpty = new HashSet<Permission>();

    private static HashSet<Permission> BuildAll()
    {
        var set = new HashSet<Permission>();
        foreach (var resource in Enum.GetValues<PermissionResource>())
        {
            foreach (var action in Enum.GetValues<PermissionAction>())
            {
                set.Add(new Permission(resource, action));
            }
        }

        return set;
    }
}

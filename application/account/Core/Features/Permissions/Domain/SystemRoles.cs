using Account.Features.Memberships.Domain;
using System.Diagnostics;

namespace Account.Features.Permissions.Domain;

/// <summary>
///     Well-known deterministic IDs for the three Nerova system roles.
///     <para>
///         These constants are referenced in migrations (<c>InsertData</c>), the test
///         <c>DatabaseSeeder</c>, and any code that needs to resolve a system role without
///         a DB round-trip (e.g., checking whether a membership has the Owner role).
///     </para>
///     <para>
///         The IDs are valid 26-character Crockford Base32 ULID strings with the <c>rol_</c>
///         prefix. They are intentionally deterministic so that references remain stable across
///         all environments and migrations.
///     </para>
/// </summary>
public static class SystemRoles
{
    /// <summary>
    ///     Fixed ID for the <c>Owner</c> system role.
    ///     Owners receive every permission (<see cref="Permission.All" />).
    /// </summary>
    public static readonly RoleId OwnerId = new("rol_00000000000000000000000001");

    /// <summary>
    ///     Fixed ID for the <c>Admin</c> system role.
    ///     Admins receive all permissions except <c>Billing.Manage</c> and <c>Organization.Delete</c>.
    /// </summary>
    public static readonly RoleId AdminId = new("rol_00000000000000000000000002");

    /// <summary>
    ///     Fixed ID for the <c>Member</c> system role.
    ///     Members receive a read/create subset scoped to their own work (bookings, schedules, event types).
    /// </summary>
    public static readonly RoleId MemberId = new("rol_00000000000000000000000003");

    // ─── Permission sets ──────────────────────────────────────────────────────

    /// <summary>All permissions — used by the Owner system role.</summary>
    public static IEnumerable<Permission> OwnerPermissions => Permission.All;

    /// <summary>
    ///     All permissions except <c>Billing.Manage</c> and <c>Organization.Delete</c>.
    ///     Used by the Admin system role.
    /// </summary>
    public static IEnumerable<Permission> AdminPermissions =>
        Permission.All.Where(p =>
            !(p.Resource == PermissionResource.Billing && p.Action == PermissionAction.Manage) &&
            !(p.Resource == PermissionResource.Organization && p.Action == PermissionAction.Delete));

    /// <summary>
    ///     Limited read/create/update permissions for everyday work.
    ///     Used by the Member system role.
    /// </summary>
    public static IEnumerable<Permission> MemberPermissions =>
    [
        new(PermissionResource.Team, PermissionAction.Read),
        new(PermissionResource.Member, PermissionAction.Read),
        new(PermissionResource.Booking, PermissionAction.Create),
        new(PermissionResource.Booking, PermissionAction.Read),
        new(PermissionResource.Booking, PermissionAction.Update),
        new(PermissionResource.EventType, PermissionAction.Create),
        new(PermissionResource.EventType, PermissionAction.Read),
        new(PermissionResource.EventType, PermissionAction.Update),
        new(PermissionResource.Schedule, PermissionAction.Create),
        new(PermissionResource.Schedule, PermissionAction.Read),
        new(PermissionResource.Schedule, PermissionAction.Update)
    ];

    // ─── Factory helpers ──────────────────────────────────────────────────────

    /// <summary>Creates the <c>Owner</c> system role with its deterministic ID and full permission set.</summary>
    public static Role CreateOwnerRole() =>
        Role.CreateSystem(OwnerId, "Owner", "Full access to all resources.", OwnerPermissions);

    /// <summary>Creates the <c>Admin</c> system role with its deterministic ID.</summary>
    public static Role CreateAdminRole() =>
        Role.CreateSystem(AdminId, "Admin", "Full access except billing management and organization deletion.", AdminPermissions);

    /// <summary>Creates the <c>Member</c> system role with its deterministic ID.</summary>
    public static Role CreateMemberRole() =>
        Role.CreateSystem(MemberId, "Member", "Day-to-day access to bookings, event types and schedules.", MemberPermissions);

    // ─── Lookup helpers ───────────────────────────────────────────────────────

    /// <summary>Maps a <see cref="MembershipRole" /> to the corresponding system role ID.</summary>
    public static RoleId GetIdForRole(MembershipRole role) => role switch
    {
        MembershipRole.Owner => OwnerId,
        MembershipRole.Admin => AdminId,
        MembershipRole.Member => MemberId,
        _ => throw new UnreachableException($"Unknown MembershipRole: {role}.")
    };

    /// <summary>Returns the static in-memory permission set for the given system <see cref="MembershipRole" />.</summary>
    public static IEnumerable<Permission> GetPermissionsForRole(MembershipRole role) => role switch
    {
        MembershipRole.Owner => OwnerPermissions,
        MembershipRole.Admin => AdminPermissions,
        MembershipRole.Member => MemberPermissions,
        _ => throw new UnreachableException($"Unknown MembershipRole: {role}.")
    };
}

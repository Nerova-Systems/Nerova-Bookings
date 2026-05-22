using JetBrains.Annotations;

namespace Account.Features.Memberships.Domain;

/// <summary>
///     Role of a <see cref="Membership" /> within a <see cref="Account.Features.Tenants.Domain.TenantKind.Team" />
///     or <see cref="Account.Features.Tenants.Domain.TenantKind.Organization" /> tenant.
///     <para>
///         Mirrors the <c>MembershipRole</c> enum from the cal.com Prisma schema
///         (<see href="https://github.com/calcom/cal.com/blob/main/packages/prisma/schema.prisma" />).
///     </para>
///     <para>
///         Bridging note: the <c>f1-pbac-domain</c> task will introduce per-organization custom roles.
///         When that lands, a nullable <c>CustomRoleId</c> FK will be added to the <c>memberships</c>
///         table alongside this enum. The custom role takes priority when set; this enum acts as the
///         coarse-grained fallback.
///     </para>
/// </summary>
[PublicAPI]
public enum MembershipRole
{
    /// <summary>Full admin: can manage members, settings, and billing. Can delete the team/org.</summary>
    Owner,

    /// <summary>Can manage members and settings, but not billing or team deletion.</summary>
    Admin,

    /// <summary>Base-level membership. No management permissions.</summary>
    Member
}

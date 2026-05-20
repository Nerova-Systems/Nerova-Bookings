namespace Account.Features.Tenants.Domain;

/// <summary>
///     Describes the structural role of a <see cref="Tenant" /> in the organizational hierarchy.
///     Replaces the boolean <c>isOrganization</c> field from the cal.com Prisma <c>Team</c> model.
///     Unlike the two-state boolean, this enum distinguishes three roles so that a flat (legacy) tenant
///     can remain <see cref="Solo" /> without being labelled either an org or a child.
/// </summary>
public enum TenantKind
{
    /// <summary>
    ///     Default flat tenant with no hierarchical affiliation.
    ///     All tenants created before the hierarchy feature have this kind.
    ///     Maps to <c>isOrganization = false</c> and <c>parentId = null</c> in the cal.com Team model.
    /// </summary>
    Solo,

    /// <summary>
    ///     A child tenant owned by an <see cref="Organization" /> parent.
    ///     Maps to <c>isOrganization = false</c> and <c>parentId != null</c> in the cal.com Team model.
    /// </summary>
    Team,

    /// <summary>
    ///     A parent tenant that can own <see cref="Team" /> children. Cannot itself have a parent (v1 restriction).
    ///     Maps to <c>isOrganization = true</c> in the cal.com Team model.
    /// </summary>
    Organization
}

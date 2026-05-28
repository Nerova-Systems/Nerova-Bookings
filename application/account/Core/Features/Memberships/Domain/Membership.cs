using Account.Features.Permissions.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Memberships.Domain;

/// <summary>
///     Strongly-typed identifier for a <see cref="Membership" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>mbr</c>.
/// </summary>
[PublicAPI]
[IdPrefix("mbr")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, MembershipId>))]
public sealed record MembershipId(string Value) : StronglyTypedUlid<MembershipId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Models the explicit membership of a <c>UserId</c> in a
///     <see cref="Account.Features.Tenants.Domain.TenantKind.Team" /> or
///     <see cref="Account.Features.Tenants.Domain.TenantKind.Organization" /> tenant.
///     Every EE feature — PBAC, OrgProfile, audit, and billing — reads from this aggregate.
///     <para>
///         Mirrors the <c>Membership</c> model in the cal.com Prisma schema
///         (<see href="https://github.com/calcom/cal.com/blob/main/packages/prisma/schema.prisma" />).
///     </para>
///     <para>
///         Intentional deviation from the domain-modeling rule "always make many-to-many aggregates
///         <see cref="ITenantScopedEntity" />": <c>Membership</c> crosses tenant boundaries — a user
///         in their Solo personal tenant can be a member of many Team/Org tenants — so the EF
///         query-filter would either produce wrong results or require constant filter suppression on
///         cross-tenant queries (e.g., "all teams I belong to"). All repository methods accept explicit
///         <c>TenantId</c> or <c>UserId</c> parameters
///         instead.
///     </para>
/// </summary>
public sealed class Membership : AggregateRoot<MembershipId>
{
    private Membership(
        MembershipId id,
        TenantId tenantId,
        UserId userId,
        MembershipRole role,
        bool accepted,
        UserId? invitedBy,
        string? inviteToken)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        Accepted = accepted;
        InvitedBy = invitedBy;
        InviteToken = inviteToken;
        AcceptedAt = accepted ? CreatedAt : null;
    }

    /// <summary>
    ///     The <see cref="Account.Features.Tenants.Domain.TenantKind.Team" /> or
    ///     <see cref="Account.Features.Tenants.Domain.TenantKind.Organization" /> this membership
    ///     belongs to. Maps to cal.com <c>Membership.teamId</c>.
    /// </summary>
    public TenantId TenantId { get; }

    /// <summary>
    ///     The user who holds this membership. Maps to cal.com <c>Membership.userId</c>.
    /// </summary>
    public UserId UserId { get; }

    /// <summary>
    ///     The role this member holds within the team/org. Maps to cal.com <c>Membership.role</c>.
    ///     <para>
    ///         The "last owner cannot demote themselves" invariant requires querying sibling memberships.
    ///         It is enforced in the command/repository layer where <see cref="IMembershipRepository" />
    ///         is available; not here.
    ///     </para>
    /// </summary>
    public MembershipRole Role { get; private set; }

    /// <summary>
    ///     <see langword="true" /> once the invitee has explicitly accepted the invitation.
    ///     <see langword="false" /> for pending (invited but not yet accepted) memberships.
    ///     Maps to cal.com <c>Membership.accepted</c>.
    ///     <para>
    ///         Invariant: <c>Accepted == true ↔ AcceptedAt != null ↔ InviteToken == null</c>.
    ///     </para>
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    ///     Cryptographically random 64-character hex token used to accept the invite via a link.
    ///     Populated on invite creation via <see cref="CreateInvite" />; cleared when
    ///     <see cref="Accept" /> is called. Null for seed-owner memberships.
    ///     Not present on the base cal.com Membership model — added to support Nerova's email-invite flow.
    /// </summary>
    public string? InviteToken { get; private set; }

    /// <summary>
    ///     Timestamp when the invitee accepted this membership. Null for pending memberships.
    ///     Not present on the base cal.com Membership model — added to support audit and accept flows.
    ///     Invariant: non-null iff <see cref="Accepted" /> is <see langword="true" />.
    /// </summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>
    ///     The user who created this invite, or <see langword="null" /> for self-created seed-owner
    ///     memberships. Not present on the base cal.com Membership model.
    /// </summary>
    public UserId? InvitedBy { get; }

    /// <summary>
    ///     When <see langword="true" />, this member has opted out of impersonation by org admins.
    ///     Maps to cal.com <c>Membership.disableImpersonation</c>.
    /// </summary>
    public bool DisableImpersonation { get; private set; }

    /// <summary>
    ///     Optional custom PBAC role override for this specific member.
    ///     When <see langword="null" />, the member's effective permissions are derived from
    ///     <see cref="Role" /> (the system role). When set, this custom role supplements or
    ///     overrides the system role's permission set for this member specifically.
    ///     Maps to the <c>custom_role_id</c> FK column in the <c>memberships</c> table.
    /// </summary>
    public RoleId? CustomRoleId { get; private set; }

    // ─── Factory methods ──────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a pending membership via the invite flow. The membership starts with
    ///     <see cref="Accepted" /><c> == false</c> and an <see cref="InviteToken" /> populated.
    ///     Call <see cref="Accept" /> when the invitee follows the invite link.
    ///     <para>
    ///         The caller is responsible for ensuring the target tenant is a Team or Organization —
    ///         Solo tenants cannot have memberships. This invariant lives in the command layer where
    ///         the <see cref="Account.Features.Tenants.Domain.Tenant" /> aggregate is available.
    ///     </para>
    /// </summary>
    /// <param name="tenantId">The Team or Organization the user is being invited to.</param>
    /// <param name="userId">The user being invited.</param>
    /// <param name="role">The role assigned to the invited member.</param>
    /// <param name="invitedBy">The user creating the invite.</param>
    /// <param name="inviteToken">
    ///     A cryptographically random token (recommended:
    ///     <c>Convert.ToHexString(RandomNumberGenerator.GetBytes(32))</c>).
    /// </param>
    public static Membership CreateInvite(
        TenantId tenantId,
        UserId userId,
        MembershipRole role,
        UserId invitedBy,
        string inviteToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inviteToken);
        return new Membership(MembershipId.NewId(), tenantId, userId, role, false, invitedBy, inviteToken);
    }

    /// <summary>
    ///     Creates an immediately accepted <see cref="MembershipRole.Owner" /> membership for the
    ///     first member of a newly created Team or Organization. No invite token is generated.
    /// </summary>
    /// <param name="tenantId">The newly created Team or Organization.</param>
    /// <param name="userId">The user who becomes the seed Owner.</param>
    public static Membership CreateSeedOwner(TenantId tenantId, UserId userId)
    {
        return new Membership(MembershipId.NewId(), tenantId, userId, MembershipRole.Owner, true, null, null);
    }

    // ─── Mutation methods ─────────────────────────────────────────────────────

    /// <summary>
    ///     Marks this membership as accepted and clears the invite token.
    /// </summary>
    /// <param name="acceptedAt">The timestamp of acceptance (caller supplies for testability).</param>
    /// <exception cref="InvalidOperationException">Thrown if the membership is already accepted.</exception>
    public void Accept(DateTimeOffset acceptedAt)
    {
        if (Accepted)
        {
            throw new InvalidOperationException("Membership is already accepted.");
        }

        Accepted = true;
        AcceptedAt = acceptedAt;
        InviteToken = null;
    }

    /// <summary>
    ///     Changes the member's role within the team/org.
    ///     <para>
    ///         The caller is responsible for ensuring this does not leave the organization without at
    ///         least one <see cref="MembershipRole.Owner" />. Use
    ///         <see cref="IMembershipRepository.CountOwnersAsync" /> before calling this method.
    ///     </para>
    /// </summary>
    /// <param name="newRole">The new role to assign.</param>
    /// <param name="changedBy">The user performing the change (reserved for future domain-event emission).</param>
    public void ChangeRole(MembershipRole newRole, UserId changedBy)
    {
        Role = newRole;
    }

    /// <summary>Updates whether this member has opted out of impersonation by org admins.</summary>
    public void SetDisableImpersonation(bool disabled)
    {
        DisableImpersonation = disabled;
    }

    /// <summary>
    ///     Assigns a custom PBAC role to this member, overriding the system role's default permissions.
    /// </summary>
    /// <param name="roleId">The custom role to assign.</param>
    public void AssignCustomRole(RoleId roleId)
    {
        ArgumentNullException.ThrowIfNull(roleId);
        CustomRoleId = roleId;
    }

    /// <summary>
    ///     Clears the custom PBAC role override, reverting this member's effective permissions back to
    ///     the system role defined by <see cref="Role" />.
    /// </summary>
    public void ClearCustomRole()
    {
        CustomRoleId = null;
    }
}

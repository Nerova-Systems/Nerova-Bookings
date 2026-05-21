using System.Text.RegularExpressions;
using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.OrgProfiles.Domain;

/// <summary>
///     Strongly-typed identifier for an <see cref="OrgProfile" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>oprf</c>.
/// </summary>
[PublicAPI]
[IdPrefix("oprf")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, OrgProfileId>))]
public sealed record OrgProfileId(string Value) : StronglyTypedUlid<OrgProfileId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     A user's per-organization display identity. Each <c>(User, Organization)</c> pair may have its
///     own username (URL slug), display name, avatar, and bio — separate from the user's global profile.
///     <para>
///         Mirrors the <c>Profile</c> model in the cal.com Prisma schema
///         (<see href="https://github.com/calcom/cal.com/blob/main/packages/prisma/schema.prisma" />).
///         The cal.com model maps a user to an organization via <c>Profile.userId / Profile.organizationId</c>.
///     </para>
///     <para>
///         Intentional similarity to <see cref="Membership" />: Membership models the
///         relationship + role; OrgProfile models the display identity in that org context. They are
///         separate aggregates to allow independent lifecycle management.
///     </para>
///     <para>
///         OrgProfile is intentionally NOT <see cref="ITenantScopedEntity" />: a user's Solo personal
///         tenant differs from any org they belong to, so the EF query filter would produce incorrect
///         results on cross-tenant lookups (e.g., "all my org profiles"). All repository methods accept
///         explicit <see cref="TenantId" /> or <see cref="UserId" /> parameters instead.
///     </para>
///     <para>
///         Invariants:
///         <list type="bullet">
///             <item>
///                 <c>OrgTenantId</c> must resolve to a <see cref="TenantKind.Organization" /> tenant
///                 (not <see cref="TenantKind.Team" />, not <see cref="TenantKind.Solo" />). Enforced
///                 in <see cref="Create" /> by passing <see cref="TenantKind" /> as a factory param.
///             </item>
///             <item><c>(UserId, OrgTenantId)</c> is unique — one profile per user per org.</item>
///             <item><c>(OrgTenantId, Username)</c> is unique — no two users share a slug within an org.</item>
///             <item>
///                 <c>Username</c> matches <c>^[a-z0-9-]+$</c>, length 1–<see cref="MaxUsernameLength" />.
///                 Matches cal.com slug rules (verified against the Profile model's username constraints).
///             </item>
///         </list>
///     </para>
/// </summary>
public sealed class OrgProfile : AggregateRoot<OrgProfileId>
{
    /// <summary>
    ///     Maximum allowed length for <see cref="Username" />.
    ///     Matches the cal.com <c>Profile.username</c> field constraint (50 chars).
    /// </summary>
    public const int MaxUsernameLength = 50;

    /// <summary>
    ///     Pattern that <see cref="Username" /> must satisfy: lowercase alphanumeric characters and
    ///     hyphens only. Matches the cal.com username/slug validation convention used across Profile,
    ///     Team, and User models. Deviates from cal.com in that we disallow leading/trailing hyphens
    ///     (cal.com also disallows them in practice via its frontend validator, but this is not
    ///     enforced at the Prisma layer; we enforce it here for safety).
    /// </summary>
    private static readonly Regex UsernamePattern =
        new("^[a-z0-9-]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private OrgProfile(
        OrgProfileId id,
        UserId userId,
        TenantId orgTenantId,
        string username,
        string? name,
        string? avatarUrl,
        string? bio)
        : base(id)
    {
        UserId = userId;
        OrgTenantId = orgTenantId;
        Username = username;
        Name = name;
        AvatarUrl = avatarUrl;
        Bio = bio;
    }

    /// <summary>
    ///     The user this profile belongs to. Maps to cal.com <c>Profile.userId</c>.
    /// </summary>
    public UserId UserId { get; }

    /// <summary>
    ///     The <see cref="TenantKind.Organization" /> this profile is scoped to.
    ///     Maps to cal.com <c>Profile.organizationId</c>.
    ///     <para>
    ///         Invariant: the referenced tenant must have <c>Kind == TenantKind.Organization</c>.
    ///         Enforced in <see cref="Create" /> via the <c>orgTenantKind</c> parameter.
    ///         This value cannot be changed after creation.
    ///     </para>
    /// </summary>
    public TenantId OrgTenantId { get; }

    /// <summary>
    ///     The per-org URL slug for this user (e.g., <c>john-doe</c>).
    ///     Used in org-subdomain booking URLs: <c>https://acme.nerova.io/john-doe</c>.
    ///     Must match <c>^[a-z0-9-]+$</c>, length 1–<see cref="MaxUsernameLength" />.
    ///     Maps to cal.com <c>Profile.username</c>.
    ///     <para>
    ///         Uniqueness within <c>(OrgTenantId, Username)</c> is enforced at the database level.
    ///         Callers should use <see cref="IOrgProfileRepository.IsUsernameAvailableAsync" /> before
    ///         calling <see cref="UpdateUsername" />.
    ///     </para>
    /// </summary>
    public string Username { get; private set; }

    /// <summary>
    ///     Per-org display name override. When <see langword="null" />, callers fall back to
    ///     <c>User.FirstName + User.LastName</c> when rendering org-context surfaces.
    ///     Maps to cal.com <c>Profile.name</c>.
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    ///     Per-org avatar URL override. When <see langword="null" />, callers fall back to
    ///     <c>User.Avatar.Url</c>.
    ///     Maps to cal.com <c>Profile.avatarUrl</c>.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>
    ///     Per-org short biography (plain text). When <see langword="null" />, callers fall back to
    ///     a user-level bio if one exists, otherwise empty.
    ///     <para>
    ///         Stored as plain text, not Markdown. Cal.com stores <c>Profile.bio</c> as a plain
    ///         <c>String?</c> in its Prisma schema; no rich-text rendering is applied at the
    ///         booking-page level. We match that behaviour.
    ///     </para>
    ///     Maps to cal.com <c>Profile.bio</c>.
    /// </summary>
    public string? Bio { get; private set; }

    // ─── Factory methods ──────────────────────────────────────────────────────

    /// <summary>
    ///     Creates a new <see cref="OrgProfile" /> for the given user in an organization.
    /// </summary>
    /// <param name="userId">The user this profile belongs to.</param>
    /// <param name="orgTenantId">The organization tenant ID.</param>
    /// <param name="orgTenantKind">
    ///     The kind of the target tenant. Callers load the <c>Tenant</c> aggregate and pass
    ///     <c>tenant.Kind</c>. The factory validates that it is <see cref="TenantKind.Organization" />.
    ///     Passing the kind (rather than the full <c>Tenant</c>) keeps the aggregate free of a
    ///     navigation dependency; mirrors the same pattern used in
    ///     <see cref="Account.Features.Permissions.Domain.Role.CreateCustom" />.
    /// </param>
    /// <param name="username">
    ///     The per-org URL slug. Must match <c>^[a-z0-9-]+$</c>, length
    ///     1–<see cref="MaxUsernameLength" />. Uniqueness within <c>(orgTenantId, username)</c> is
    ///     enforced at the DB level.
    /// </param>
    /// <param name="name">Optional display name override; <see langword="null" /> falls back to user name.</param>
    /// <param name="avatarUrl">Optional avatar URL override; <see langword="null" /> falls back to user avatar.</param>
    /// <param name="bio">Optional bio (plain text); <see langword="null" /> falls back to user-level bio.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="orgTenantKind" /> is not <see cref="TenantKind.Organization" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="username" /> is empty, exceeds <see cref="MaxUsernameLength" />,
    ///     or contains characters not matching <c>^[a-z0-9-]+$</c>.
    /// </exception>
    public static OrgProfile Create(
        UserId userId,
        TenantId orgTenantId,
        TenantKind orgTenantKind,
        string username,
        string? name,
        string? avatarUrl,
        string? bio)
    {
        if (orgTenantKind != TenantKind.Organization)
        {
            throw new InvalidOperationException(
                $"OrgProfile can only be created for an Organization tenant, but the provided tenant has kind '{orgTenantKind}'."
            );
        }

        ValidateUsername(username);

        return new OrgProfile(OrgProfileId.NewId(), userId, orgTenantId, username, name, avatarUrl, bio);
    }

    // ─── Mutation methods ─────────────────────────────────────────────────────

    /// <summary>
    ///     Changes the per-org URL slug for this user.
    ///     <para>
    ///         The caller is responsible for checking uniqueness within the org before calling this.
    ///         Use <see cref="IOrgProfileRepository.IsUsernameAvailableAsync" /> first.
    ///     </para>
    /// </summary>
    /// <param name="newUsername">New slug value. Must match <c>^[a-z0-9-]+$</c>, length 1–<see cref="MaxUsernameLength" />.</param>
    /// <exception cref="ArgumentException">Thrown when the new username is invalid.</exception>
    public void UpdateUsername(string newUsername)
    {
        ValidateUsername(newUsername);
        Username = newUsername;
    }

    /// <summary>
    ///     Updates the per-org display fields in a single call.
    ///     Pass <see langword="null" /> for any field to clear the override and revert to the global
    ///     user fallback.
    /// </summary>
    /// <param name="name">New display name override, or <see langword="null" /> to clear.</param>
    /// <param name="avatarUrl">New avatar URL override, or <see langword="null" /> to clear.</param>
    /// <param name="bio">New bio (plain text), or <see langword="null" /> to clear.</param>
    public void UpdateDisplay(string? name, string? avatarUrl, string? bio)
    {
        Name = name;
        AvatarUrl = avatarUrl;
        Bio = bio;
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty or whitespace.", nameof(username));
        }

        if (username.Length > MaxUsernameLength)
        {
            throw new ArgumentException(
                $"Username must not exceed {MaxUsernameLength} characters; got {username.Length}.", nameof(username)
            );
        }

        if (!UsernamePattern.IsMatch(username))
        {
            throw new ArgumentException(
                "Username must match '^[a-z0-9-]+$' (lowercase letters, digits, and hyphens only).",
                nameof(username)
            );
        }
    }
}

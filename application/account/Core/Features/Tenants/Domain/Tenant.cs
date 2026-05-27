using Account.Features.Subscriptions.Domain;
using SharedKernel.Domain;
using SharedKernel.FeatureFlags;

namespace Account.Features.Tenants.Domain;

public sealed class Tenant : SoftDeletableAggregateRoot<TenantId>
{
    private Tenant(int rolloutBucket, TenantKind kind = TenantKind.Solo, TenantId? parentTenantId = null) : base(TenantId.NewId())
    {
        Kind = kind;
        ParentTenantId = parentTenantId;
        State = TenantState.Active;
        Plan = SubscriptionPlan.Basis;
        Logo = new Logo();
        RolloutBucket = rolloutBucket;
    }

    public string Name { get; private set; } = string.Empty;

    public TenantState State { get; private set; }

    public SubscriptionPlan Plan { get; private set; }

    public SuspensionReason? SuspensionReason { get; private set; }

    public DateTimeOffset? SuspendedAt { get; private set; }

    public Logo Logo { get; private set; }

    public int RolloutBucket { get; private set; }

    public AbInclusionPin? AbInclusionPin { get; private set; }

    /// <summary>
    ///     The tenant's WhatsApp-facing brand profile. <see langword="null" /> until the tenant
    ///     calls <see cref="Commands.UpdateTenantBrandProfileCommand" />. Persisted as an EF
    ///     owned-type stored in a single <c>jsonb</c> column (<c>brand_profile</c>) for
    ///     forward-compatibility with future profile fields.
    /// </summary>
    public BrandProfile? BrandProfile { get; private set; }

    /// <summary>
    ///     URL-friendly identifier used for team and organization profile pages.
    ///     Null for <see cref="TenantKind.Solo" /> tenants, which have no shared booking page.
    ///     Unique among organizations globally; unique within a parent organization for teams.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.slug</see>
    /// </summary>
    public string? Slug { get; private set; }

    /// <summary>
    ///     Short description shown on the team or organization profile page.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.bio</see>
    /// </summary>
    public string? Bio { get; private set; }

    /// <summary>
    ///     When <see langword="true" />, Nerova branding is hidden on the team's booking pages.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.hideBranding</see>
    /// </summary>
    public bool HideBranding { get; private set; }

    /// <summary>
    ///     When <see langword="true" />, the public link to the team's profile page is hidden.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.hideTeamProfileLink</see>
    /// </summary>
    public bool HideTeamProfileLink { get; private set; }

    /// <summary>
    ///     When <see langword="true" />, the team is not discoverable in public listings.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.isPrivate</see>
    /// </summary>
    public bool IsPrivate { get; private set; }

    /// <summary>
    ///     When <see langword="true" />, hides the "Book a team member" option on the team page.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.hideBookATeamMember</see>
    /// </summary>
    public bool HideBookATeamMember { get; private set; }

    /// <summary>
    ///     UI theme applied to this team's booking pages. Null means use the system default.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.theme</see>
    /// </summary>
    public string? Theme { get; private set; }

    /// <summary>
    ///     Primary brand color (hex string) shown on booking pages.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.brandColor</see>
    /// </summary>
    public string? BrandColor { get; private set; }

    /// <summary>
    ///     Brand color used when the UI is in dark mode.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.darkBrandColor</see>
    /// </summary>
    public string? DarkBrandColor { get; private set; }

    /// <summary>
    ///     Preferred time format: 12 or 24 hours. Null means respect the booker's locale.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.timeFormat</see>
    /// </summary>
    public int? TimeFormat { get; private set; }

    /// <summary>
    ///     IANA time-zone identifier for this team's scheduling context (e.g., "Europe/London").
    ///     Null for <see cref="TenantKind.Solo" /> tenants; defaults to "Europe/London" for teams/orgs.
    ///     Deviates from cal.com (non-nullable with default) — made nullable so Solo tenants require no
    ///     default value while staying backward-compatible.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.timeZone</see>
    /// </summary>
    public string? TimeZone { get; private set; }

    /// <summary>
    ///     First day of the week for calendar display (e.g., "Sunday", "Monday").
    ///     Null for <see cref="TenantKind.Solo" /> tenants; defaults to "Sunday" for teams/orgs.
    ///     Deviates from cal.com (non-nullable with default) — made nullable so Solo tenants require no
    ///     default value while staying backward-compatible.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.weekStart</see>
    /// </summary>
    public string? WeekStart { get; private set; }

    /// <summary>
    ///     The structural role of this tenant in the organizational hierarchy.
    ///     Replaces cal.com's boolean <c>isOrganization</c> field with a three-state enum so that
    ///     pre-hierarchy (Solo) tenants are distinguished from Teams and Organizations.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.isOrganization</see>
    /// </summary>
    public TenantKind Kind { get; }

    /// <summary>
    ///     The ID of the parent <see cref="TenantKind.Organization" /> tenant when this tenant is a
    ///     <see cref="TenantKind.Team" />; <see langword="null" /> otherwise.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.parentId</see>
    /// </summary>
    public TenantId? ParentTenantId { get; private set; }

    public static Tenant Create(string email, int existingCount)
    {
        var tenant = new Tenant(RolloutBucketHasher.ComputeRolloutBucket(existingCount));
        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, email));
        return tenant;
    }

    /// <summary>
    ///     Creates an <see cref="TenantKind.Organization" /> tenant that can own child teams.
    ///     Organizations have no parent (v1 restriction).
    /// </summary>
    public static Tenant CreateOrganization(string email, int existingCount)
    {
        var tenant = new Tenant(RolloutBucketHasher.ComputeRolloutBucket(existingCount), TenantKind.Organization)
        {
            TimeZone = "Europe/London",
            WeekStart = "Sunday"
        };
        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, email));
        return tenant;
    }

    /// <summary>
    ///     Creates a <see cref="TenantKind.Team" /> tenant that is a child of <paramref name="parent" />.
    /// </summary>
    /// <param name="parent">
    ///     The owning organization. Must have <see cref="TenantKind.Organization" /> kind;
    ///     passing a <see cref="TenantKind.Solo" /> or <see cref="TenantKind.Team" /> parent throws.
    /// </param>
    /// <param name="existingCount">Monotonic index used to compute the rollout bucket.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="parent" /> is not an Organization.
    /// </exception>
    public static Tenant CreateTeam(Tenant parent, int existingCount)
    {
        if (parent.Kind == TenantKind.Team)
        {
            throw new InvalidOperationException(
                "A Team cannot be nested under another Team tenant."
            );
        }

        return new Tenant(RolloutBucketHasher.ComputeRolloutBucket(existingCount), TenantKind.Team, parent.Id)
        {
            TimeZone = "Europe/London",
            WeekStart = "Sunday"
        };
    }

    public void Suspend(SuspensionReason reason, DateTimeOffset suspendedAt)
    {
        State = TenantState.Suspended;
        SuspensionReason = reason;
        SuspendedAt = suspendedAt;
    }

    public void Activate()
    {
        State = TenantState.Active;
        SuspensionReason = null;
        SuspendedAt = null;
    }

    public void Update(string tenantName)
    {
        Name = tenantName;
    }

    /// <summary>
    ///     Sets or clears the URL slug for this team or organization.
    ///     Uniqueness is enforced at the database level; this method performs no duplicate check.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called on a <see cref="TenantKind.Solo" /> tenant.</exception>
    public void SetSlug(string? slug)
    {
        if (Kind == TenantKind.Solo)
        {
            throw new InvalidOperationException("Solo tenants do not have a slug. Only Teams and Organizations can have a slug.");
        }

        Slug = slug;
    }

    /// <summary>
    ///     Updates the branding and scheduling preferences for this team or organization.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called on a <see cref="TenantKind.Solo" /> tenant.</exception>
    public void UpdateBranding(
        string? bio,
        bool hideBranding,
        bool hideTeamProfileLink,
        bool isPrivate,
        bool hideBookATeamMember,
        string? theme,
        string? brandColor,
        string? darkBrandColor,
        int? timeFormat,
        string? timeZone,
        string? weekStart)
    {
        if (Kind == TenantKind.Solo)
        {
            throw new InvalidOperationException("Solo tenants cannot have team branding settings.");
        }

        Bio = bio;
        HideBranding = hideBranding;
        HideTeamProfileLink = hideTeamProfileLink;
        IsPrivate = isPrivate;
        HideBookATeamMember = hideBookATeamMember;
        Theme = theme;
        BrandColor = brandColor;
        DarkBrandColor = darkBrandColor;
        TimeFormat = timeFormat;
        TimeZone = timeZone;
        WeekStart = weekStart;
    }

    public void UpdateLogo(string logoUrl)
    {
        Logo = new Logo(logoUrl, Logo.Version + 1);
    }

    public void RemoveLogo()
    {
        Logo = new Logo(Version: Logo.Version);
    }

    public void UpdatePlan(SubscriptionPlan plan)
    {
        Plan = plan;
    }

    public void SetAbInclusionPin(AbInclusionPin? abInclusionPin)
    {
        AbInclusionPin = abInclusionPin;
    }

    /// <summary>
    ///     Replaces the entire <see cref="BrandProfile" />. Callers are expected to construct the
    ///     value object via <see cref="Domain.BrandProfile.Create" /> first, so all validation has
    ///     already passed by the time this is called.
    /// </summary>
    public void UpdateBrandProfile(BrandProfile brandProfile)
    {
        BrandProfile = brandProfile;
    }
}

public sealed record Logo(string? Url = null, int Version = 0);

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
    ///     The structural role of this tenant in the organizational hierarchy.
    ///     Replaces cal.com's boolean <c>isOrganization</c> field with a three-state enum so that
    ///     pre-hierarchy (Solo) tenants are distinguished from Teams and Organizations.
    ///     <see href="cal.com/packages/prisma/schema.prisma">Team.isOrganization</see>
    /// </summary>
    public TenantKind Kind { get; private set; }

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
        var tenant = new Tenant(RolloutBucketHasher.ComputeRolloutBucket(existingCount), TenantKind.Organization);
        tenant.AddDomainEvent(new TenantCreatedEvent(tenant.Id, email));
        return tenant;
    }

    /// <summary>
    ///     Creates a <see cref="TenantKind.Team" /> tenant that is a child of <paramref name="parentOrg" />.
    /// </summary>
    /// <param name="parentOrg">
    ///     The owning organization. Must have <see cref="TenantKind.Organization" /> kind;
    ///     passing a <see cref="TenantKind.Solo" /> or <see cref="TenantKind.Team" /> parent throws.
    /// </param>
    /// <param name="existingCount">Monotonic index used to compute the rollout bucket.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="parentOrg" /> is not an Organization.
    /// </exception>
    public static Tenant CreateTeam(Tenant parentOrg, int existingCount)
    {
        if (parentOrg.Kind != TenantKind.Organization)
            throw new InvalidOperationException(
                $"A Team can only be created under an Organization tenant, but the provided parent has kind '{parentOrg.Kind}'.");

        return new Tenant(RolloutBucketHasher.ComputeRolloutBucket(existingCount), TenantKind.Team, parentOrg.Id);
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
}

public sealed record Logo(string? Url = null, int Version = 0);

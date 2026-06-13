using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.OrgProfiles.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.FeatureFlags;
using FeatureFlagAggregate = Account.Features.FeatureFlags.Domain.FeatureFlag;

namespace Account.Workers;

/// <summary>
///     Seeds a ready-to-use demo organization into the local development database so teams and
///     organization functionality is immediately visible and testable: an organization with an owner, two
///     teams with memberships, an org profile, and active tier flags (tier-teams / tier-organizations are
///     kill-switch flags that would otherwise hide all team functionality until manually activated).
///     Idempotent — re-running on an already-seeded database is a no-op. Never runs in Azure.
/// </summary>
public sealed class DevelopmentDataSeeder(AccountDbContext accountDbContext, TimeProvider timeProvider, ILogger<DevelopmentDataSeeder> logger)
{
    private const string DemoOwnerEmail = "owner@glow-demo.dev";
    private const string DemoMemberEmail = "stylist@glow-demo.dev";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var alreadySeeded = await accountDbContext.Set<User>().IgnoreQueryFilters().AnyAsync(user => user.Email == DemoOwnerEmail, cancellationToken);
        if (alreadySeeded)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var existingTenantCount = await accountDbContext.Set<Tenant>().IgnoreQueryFilters().CountAsync(cancellationToken);

        var organization = Tenant.CreateOrganization(DemoOwnerEmail, existingTenantCount);
        organization.Update("Glow Beauty Group");
        organization.SetSlug("glow-beauty-group");
        accountDbContext.Set<Tenant>().Add(organization);

        var owner = User.Create(organization.Id, DemoOwnerEmail, UserRole.Owner, true, "en-US", 0);
        var member = User.Create(organization.Id, DemoMemberEmail, UserRole.Member, true, "en-US", 1);
        accountDbContext.Set<User>().AddRange(owner, member);

        var sandtonTeam = Tenant.CreateTeam(organization, existingTenantCount + 1);
        sandtonTeam.Update("Glow Salon Sandton");
        sandtonTeam.SetSlug("glow-sandton");
        var rosebankTeam = Tenant.CreateTeam(organization, existingTenantCount + 2);
        rosebankTeam.Update("Glow Salon Rosebank");
        rosebankTeam.SetSlug("glow-rosebank");
        accountDbContext.Set<Tenant>().AddRange(sandtonTeam, rosebankTeam);

        // Tenants and users must be committed before memberships: the membership→user foreign key
        // exists only in the database (not in the EF model), so a single batched SaveChanges may
        // order the membership inserts first and violate the constraint on PostgreSQL.
        await accountDbContext.SaveChangesAsync(cancellationToken);

        accountDbContext.Set<Membership>().Add(Membership.CreateSeedOwner(organization.Id, owner.Id));
        accountDbContext.Set<Membership>().Add(Membership.CreateSeedOwner(sandtonTeam.Id, owner.Id));
        accountDbContext.Set<Membership>().Add(Membership.CreateSeedOwner(rosebankTeam.Id, owner.Id));

        var memberInvite = Membership.CreateInvite(sandtonTeam.Id, member.Id, MembershipRole.Member, owner.Id, Guid.NewGuid().ToString("N"));
        memberInvite.Accept(now);
        accountDbContext.Set<Membership>().Add(memberInvite);

        accountDbContext.Set<OrgProfile>().Add(OrgProfile.Create(owner.Id, organization.Id, organization.Kind, "glow-owner", "Glow Owner", null, "Owner of Glow Beauty Group"));

        await ActivateTierFlagForDemoOrgAsync(FeatureFlags.TierTeams.Key, organization.Id, now, cancellationToken);
        await ActivateTierFlagForDemoOrgAsync(FeatureFlags.TierOrganizations.Key, organization.Id, now, cancellationToken);

        await accountDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded development demo organization '{OrganizationName}' with two teams", organization.Name);
    }

    private async Task ActivateTierFlagForDemoOrgAsync(string flagKey, TenantId organizationId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // The reconciler creates kill-switch base rows inactive; activate the base flag and grant the
        // demo org an enabled override so team functionality is visible out of the box in development.
        var baseFlag = await accountDbContext.Set<FeatureFlagAggregate>()
            .FirstOrDefaultAsync(flag => flag.FlagKey == flagKey && flag.TenantId == null && flag.UserId == null, cancellationToken);
        if (baseFlag is null)
        {
            baseFlag = FeatureFlagAggregate.Create(flagKey, FeatureFlagScope.Tenant);
            accountDbContext.Set<FeatureFlagAggregate>().Add(baseFlag);
        }

        if (!baseFlag.IsActive)
        {
            baseFlag.Activate(now);
        }

        var overrideFlag = FeatureFlagAggregate.CreateTenantOverride(flagKey, organizationId, FeatureFlagScope.Tenant);
        overrideFlag.Activate(now);
        accountDbContext.Set<FeatureFlagAggregate>().Add(overrideFlag);
    }
}

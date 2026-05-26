using Account.Database;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Account.Tests.Tenants;

/// <summary>
///     Covers the team-specific fields added to <see cref="Tenant" /> in task f1-team-aggregate.
///     Tests fall into three categories:
///     <list type="number">
///         <item>Domain invariants — pure in-memory assertions, no database round-trip.</item>
///         <item>Repository queries — save via EF, assert via <see cref="ITenantRepository" />.</item>
///         <item>Slug uniqueness — database-level unique constraint enforcement.</item>
///     </list>
/// </summary>
public sealed class TenantAggregateFieldsTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // Domain invariants (no database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateOrganization_ShouldInitializeSchedulingDefaults()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);

        org.TimeZone.Should().Be("Europe/London");
        org.WeekStart.Should().Be("Sunday");
    }

    [Fact]
    public void CreateTeam_ShouldInitializeSchedulingDefaults()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);
        var team = Tenant.CreateTeam(org, 1);

        team.TimeZone.Should().Be("Europe/London");
        team.WeekStart.Should().Be("Sunday");
    }

    [Fact]
    public void Create_Solo_ShouldHaveNullBrandingAndSchedulingFields()
    {
        var solo = Tenant.Create("owner@test.com", 0);

        solo.Slug.Should().BeNull();
        solo.Bio.Should().BeNull();
        solo.HideBranding.Should().BeFalse();
        solo.HideTeamProfileLink.Should().BeFalse();
        solo.IsPrivate.Should().BeFalse();
        solo.HideBookATeamMember.Should().BeFalse();
        solo.Theme.Should().BeNull();
        solo.BrandColor.Should().BeNull();
        solo.DarkBrandColor.Should().BeNull();
        solo.TimeFormat.Should().BeNull();
        solo.TimeZone.Should().BeNull();
        solo.WeekStart.Should().BeNull();
    }

    [Fact]
    public void SetSlug_WhenSolo_ShouldThrowInvalidOperationException()
    {
        var solo = Tenant.Create("owner@test.com", 0);

        var act = () => solo.SetSlug("my-slug");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Solo*");
    }

    [Fact]
    public void SetSlug_WhenOrganization_ShouldSetSlug()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);

        org.SetSlug("acme");

        org.Slug.Should().Be("acme");
    }

    [Fact]
    public void SetSlug_WhenTeam_ShouldSetSlug()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);
        var team = Tenant.CreateTeam(org, 1);

        team.SetSlug("engineering");

        team.Slug.Should().Be("engineering");
    }

    [Fact]
    public void SetSlug_WhenCalledWithNull_ShouldClearSlug()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);
        org.SetSlug("acme");

        org.SetSlug(null);

        org.Slug.Should().BeNull();
    }

    [Fact]
    public void UpdateBranding_WhenSolo_ShouldThrowInvalidOperationException()
    {
        var solo = Tenant.Create("owner@test.com", 0);

        var act = () => solo.UpdateBranding(
            "Bio",
            true,
            false,
            false,
            false,
            null,
            null,
            null,
            null,
            null,
            null
        );

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Solo*");
    }

    [Fact]
    public void UpdateBranding_WhenOrganization_ShouldUpdateAllFields()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);

        org.UpdateBranding(
            "We build things",
            true,
            true,
            true,
            true,
            "dark",
            "#FF0000",
            "#AA0000",
            24,
            "Africa/Johannesburg",
            "Monday"
        );

        org.Bio.Should().Be("We build things");
        org.HideBranding.Should().BeTrue();
        org.HideTeamProfileLink.Should().BeTrue();
        org.IsPrivate.Should().BeTrue();
        org.HideBookATeamMember.Should().BeTrue();
        org.Theme.Should().Be("dark");
        org.BrandColor.Should().Be("#FF0000");
        org.DarkBrandColor.Should().Be("#AA0000");
        org.TimeFormat.Should().Be(24);
        org.TimeZone.Should().Be("Africa/Johannesburg");
        org.WeekStart.Should().Be("Monday");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Repository round-trip (database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BrandingFields_RoundTrip_ShouldPersistAndReloadCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var org = Tenant.CreateOrganization("admin@branding.com", 10);
        org.SetSlug("branding-org");
        org.UpdateBranding(
            "We build things",
            true,
            true,
            true,
            true,
            "dark",
            "#112233",
            "#AABBCC",
            12,
            "America/New_York",
            "Monday"
        );
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        // Act
        var loaded = await repository.GetByIdAsync(org.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded.Slug.Should().Be("branding-org");
        loaded.Bio.Should().Be("We build things");
        loaded.HideBranding.Should().BeTrue();
        loaded.HideTeamProfileLink.Should().BeTrue();
        loaded.IsPrivate.Should().BeTrue();
        loaded.HideBookATeamMember.Should().BeTrue();
        loaded.Theme.Should().Be("dark");
        loaded.BrandColor.Should().Be("#112233");
        loaded.DarkBrandColor.Should().Be("#AABBCC");
        loaded.TimeFormat.Should().Be(12);
        loaded.TimeZone.Should().Be("America/New_York");
        loaded.WeekStart.Should().Be("Monday");
    }

    [Fact]
    public async Task ExistingSoloTenant_WhenReloaded_ShouldHaveNullBrandingFields()
    {
        // Backward compat: tenants created before the team-aggregate feature must load with null/false defaults.
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var tenant = await repository.GetByIdAsync(DatabaseSeeder.Tenant1.Id, CancellationToken.None);

        tenant.Should().NotBeNull();
        tenant.Kind.Should().Be(TenantKind.Solo);
        tenant.Slug.Should().BeNull();
        tenant.Bio.Should().BeNull();
        tenant.HideBranding.Should().BeFalse();
        tenant.HideTeamProfileLink.Should().BeFalse();
        tenant.IsPrivate.Should().BeFalse();
        tenant.HideBookATeamMember.Should().BeFalse();
        tenant.Theme.Should().BeNull();
        tenant.BrandColor.Should().BeNull();
        tenant.DarkBrandColor.Should().BeNull();
        tenant.TimeFormat.Should().BeNull();
        tenant.TimeZone.Should().BeNull();
        tenant.WeekStart.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_WhenOrgHasSlug_ShouldReturnOrg()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var org = Tenant.CreateOrganization("admin@sluglookup.com", 20);
        org.SetSlug("my-org");
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetBySlugAsync("my-org", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(org.Id);
        result.Kind.Should().Be(TenantKind.Organization);
    }

    [Fact]
    public async Task GetBySlugAsync_WhenSlugDoesNotExist_ShouldReturnNull()
    {
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var result = await repository.GetBySlugAsync("nonexistent-slug", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTeamBySlugInOrgAsync_WhenTeamHasSlug_ShouldReturnTeam()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var org = Tenant.CreateOrganization("admin@teamslug.com", 30);
        var team = Tenant.CreateTeam(org, 31);
        team.SetSlug("engineering");
        dbContext.Set<Tenant>().AddRange(org, team);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetTeamBySlugInOrgAsync(org.Id, "engineering", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(team.Id);
        result.Kind.Should().Be(TenantKind.Team);
    }

    [Fact]
    public async Task GetTeamBySlugInOrgAsync_WhenSlugBelongsToDifferentOrg_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var org1 = Tenant.CreateOrganization("admin@org1.com", 40);
        var org2 = Tenant.CreateOrganization("admin@org2.com", 41);
        var team = Tenant.CreateTeam(org1, 42);
        team.SetSlug("shared-slug");
        dbContext.Set<Tenant>().AddRange(org1, org2, team);
        await dbContext.SaveChangesAsync();

        // Act — look up the slug under org2, which does not own the team
        var result = await repository.GetTeamBySlugInOrgAsync(org2.Id, "shared-slug", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Slug uniqueness (database-level constraint tests)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SlugUniqueness_TwoOrgsCannotShareSlug_ShouldThrow()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org1 = Tenant.CreateOrganization("admin@orgA.com", 50);
        org1.SetSlug("duplicate-slug");

        var org2 = Tenant.CreateOrganization("admin@orgB.com", 51);
        org2.SetSlug("duplicate-slug");

        dbContext.Set<Tenant>().Add(org1);
        await dbContext.SaveChangesAsync();
        dbContext.Set<Tenant>().Add(org2);

        // Act + Assert
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SlugUniqueness_TwoTeamsUnderSameOrgCannotShareSlug_ShouldThrow()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org = Tenant.CreateOrganization("admin@sharedorg.com", 60);
        var team1 = Tenant.CreateTeam(org, 61);
        team1.SetSlug("same-slug");
        var team2 = Tenant.CreateTeam(org, 62);
        team2.SetSlug("same-slug");

        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();
        dbContext.Set<Tenant>().Add(team1);
        await dbContext.SaveChangesAsync();
        dbContext.Set<Tenant>().Add(team2);

        // Act + Assert
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SlugUniqueness_TwoTeamsUnderDifferentOrgsCanShareSlug_ShouldSucceed()
    {
        // Arrange — same slug "shared" is allowed across different parent organizations
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org1 = Tenant.CreateOrganization("admin@org-x.com", 70);
        var org2 = Tenant.CreateOrganization("admin@org-y.com", 71);
        var teamUnderOrg1 = Tenant.CreateTeam(org1, 72);
        teamUnderOrg1.SetSlug("shared");
        var teamUnderOrg2 = Tenant.CreateTeam(org2, 73);
        teamUnderOrg2.SetSlug("shared");

        dbContext.Set<Tenant>().AddRange(org1, org2, teamUnderOrg1, teamUnderOrg2);

        // Act + Assert — should not throw
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}

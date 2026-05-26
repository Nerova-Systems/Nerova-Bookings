using Account.Database;
using Account.Features.OrgProfiles.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Account.Tests.OrgProfiles;

/// <summary>
///     Covers the <see cref="OrgProfile" /> aggregate introduced in task <c>f1-org-profile</c>.
///     Tests fall into three categories:
///     <list type="number">
///         <item>Domain invariants — pure in-memory assertions, no database round-trip.</item>
///         <item>Repository queries — save via EF, assert via <see cref="IOrgProfileRepository" />.</item>
///         <item>Uniqueness constraints — database-level unique constraint enforcement.</item>
///     </list>
/// </summary>
public sealed class OrgProfileAggregateTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // Domain invariants (no database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ForOrganizationTenant_ShouldSetFieldsCorrectly()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;

        var profile = OrgProfile.Create(userId, org.Id, TenantKind.Organization, "john-doe", "John Doe", "https://example.com/avatar.png", "Bio text");

        profile.UserId.Should().Be(userId);
        profile.OrgTenantId.Should().Be(org.Id);
        profile.Username.Should().Be("john-doe");
        profile.Name.Should().Be("John Doe");
        profile.AvatarUrl.Should().Be("https://example.com/avatar.png");
        profile.Bio.Should().Be("Bio text");
    }

    [Fact]
    public void Create_WithNullOptionalFields_ShouldSucceed()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;

        var profile = OrgProfile.Create(userId, org.Id, TenantKind.Organization, "jane123", null, null, null);

        profile.Name.Should().BeNull();
        profile.AvatarUrl.Should().BeNull();
        profile.Bio.Should().BeNull();
    }

    [Fact]
    public void Create_ForSoloTenant_ShouldThrowInvalidOperationException()
    {
        var userId = DatabaseSeeder.Tenant1Owner.Id;
        var soloTenantId = DatabaseSeeder.Tenant1.Id;

        var act = () => OrgProfile.Create(userId, soloTenantId, TenantKind.Solo, "john-doe", null, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Organization*");
    }

    [Fact]
    public void Create_ForTeamTenant_ShouldThrowInvalidOperationException()
    {
        var userId = DatabaseSeeder.Tenant1Owner.Id;
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var team = Tenant.CreateTeam(org, 1);

        var act = () => OrgProfile.Create(userId, team.Id, TenantKind.Team, "john-doe", null, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Organization*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceUsername_ShouldThrowArgumentException(string username)
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;

        var act = () => OrgProfile.Create(userId, org.Id, TenantKind.Organization, username, null, null, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithUppercaseUsername_ShouldThrowArgumentException()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;

        var act = () => OrgProfile.Create(userId, org.Id, TenantKind.Organization, "John-Doe", null, null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*[a-z0-9-]*");
    }

    [Fact]
    public void Create_WithSpecialCharactersInUsername_ShouldThrowArgumentException()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;

        var act = () => OrgProfile.Create(userId, org.Id, TenantKind.Organization, "john_doe", null, null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*[a-z0-9-]*");
    }

    [Fact]
    public void Create_WithTooLongUsername_ShouldThrowArgumentException()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;
        var tooLong = new string('a', OrgProfile.MaxUsernameLength + 1);

        var act = () => OrgProfile.Create(userId, org.Id, TenantKind.Organization, tooLong, null, null, null);

        act.Should().Throw<ArgumentException>().WithMessage("*exceed*");
    }

    [Theory]
    [InlineData("john-doe")]
    [InlineData("jane123")]
    [InlineData("a")]
    [InlineData("abc-def-123")]
    public void Create_WithValidUsername_ShouldSucceed(string username)
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;

        var act = () => OrgProfile.Create(userId, org.Id, TenantKind.Organization, username, null, null, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateUsername_WithValidUsername_ShouldChangeSlug()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var profile = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "old-slug", null, null, null);

        profile.UpdateUsername("new-slug");

        profile.Username.Should().Be("new-slug");
    }

    [Fact]
    public void UpdateUsername_WithInvalidUsername_ShouldThrowArgumentException()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var profile = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "old-slug", null, null, null);

        var act = () => profile.UpdateUsername("Invalid_Slug!");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDisplay_ShouldUpdateAllFields()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var profile = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "john-doe", "Old Name", "https://old.com/avatar.png", "Old bio");

        profile.UpdateDisplay("New Name", "https://new.com/avatar.png", "New bio");

        profile.Name.Should().Be("New Name");
        profile.AvatarUrl.Should().Be("https://new.com/avatar.png");
        profile.Bio.Should().Be("New bio");
    }

    [Fact]
    public void UpdateDisplay_WithNullValues_ShouldClearFields()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var profile = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "john-doe", "Name", "https://example.com/avatar.png", "Bio");

        profile.UpdateDisplay(null, null, null);

        profile.Name.Should().BeNull();
        profile.AvatarUrl.Should().BeNull();
        profile.Bio.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Repository round-trip (database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_RoundTrip_ShouldPersistAndReloadCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org = Tenant.CreateOrganization("roundtrip@example.com", 10);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var profile = OrgProfile.Create(
            DatabaseSeeder.Tenant1Owner.Id,
            org.Id,
            TenantKind.Organization,
            "john-doe",
            "John Doe",
            "https://example.com/avatar.png",
            "Hello, world!"
        );
        await repository.AddAsync(profile, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var loaded = await repository.GetByIdAsync(profile.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded.UserId.Should().Be(DatabaseSeeder.Tenant1Owner.Id);
        loaded.OrgTenantId.Should().Be(org.Id);
        loaded.Username.Should().Be("john-doe");
        loaded.Name.Should().Be("John Doe");
        loaded.AvatarUrl.Should().Be("https://example.com/avatar.png");
        loaded.Bio.Should().Be("Hello, world!");
    }

    [Fact]
    public async Task GetByOrgAndUsernameAsync_WhenProfileExists_ShouldReturnProfile()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org = Tenant.CreateOrganization("byorgusername@example.com", 11);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var profile = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "jane-smith", null, null, null);
        await repository.AddAsync(profile, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByOrgAndUsernameAsync(org.Id, "jane-smith", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(profile.Id);
    }

    [Fact]
    public async Task GetByOrgAndUsernameAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org = Tenant.CreateOrganization("notfoundusername@example.com", 12);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByOrgAndUsernameAsync(org.Id, "nonexistent-slug", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserAsync_ShouldReturnAllProfilesForUser()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org1 = Tenant.CreateOrganization("userprofiles-a@example.com", 20);
        var org2 = Tenant.CreateOrganization("userprofiles-b@example.com", 21);
        dbContext.Set<Tenant>().AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var p1 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org1.Id, TenantKind.Organization, "slug-org1", null, null, null);
        var p2 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org2.Id, TenantKind.Organization, "slug-org2", null, null, null);
        dbContext.Set<OrgProfile>().AddRange(p1, p2);
        await dbContext.SaveChangesAsync();

        // Act
        var profiles = await repository.GetByUserAsync(DatabaseSeeder.Tenant1Owner.Id, CancellationToken.None);

        // Assert — both profiles returned, crosses two org tenants
        profiles.Should().HaveCount(2);
        profiles.Select(p => p.OrgTenantId).Should().BeEquivalentTo([org1.Id, org2.Id]);
    }

    [Fact]
    public async Task GetMembersOfOrgAsync_ShouldReturnAllProfilesInOrg()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org = Tenant.CreateOrganization("orgmembers@example.com", 22);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var p1 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "owner-slug", null, null, null);
        var p2 = OrgProfile.Create(DatabaseSeeder.Tenant1Member.Id, org.Id, TenantKind.Organization, "member-slug", null, null, null);
        dbContext.Set<OrgProfile>().AddRange(p1, p2);
        await dbContext.SaveChangesAsync();

        // Act
        var members = await repository.GetMembersOfOrgAsync(org.Id, CancellationToken.None);

        // Assert
        members.Should().HaveCount(2);
        members.Select(p => p.Username).Should().BeEquivalentTo("owner-slug", "member-slug");
    }

    [Fact]
    public async Task IsUsernameAvailableAsync_WhenUsernameNotTaken_ShouldReturnTrue()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org = Tenant.CreateOrganization("usernamecheck-a@example.com", 23);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        // Act
        var available = await repository.IsUsernameAvailableAsync(org.Id, "brand-new-slug", null, CancellationToken.None);

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsUsernameAvailableAsync_WhenUsernameTaken_ShouldReturnFalse()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org = Tenant.CreateOrganization("usernamecheck-b@example.com", 24);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var profile = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "taken-slug", null, null, null);
        await repository.AddAsync(profile, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var available = await repository.IsUsernameAvailableAsync(org.Id, "taken-slug", null, CancellationToken.None);

        // Assert
        available.Should().BeFalse();
    }

    [Fact]
    public async Task IsUsernameAvailableAsync_WhenExcludingOwnerProfile_ShouldReturnTrue()
    {
        // Arrange — updating a profile should not conflict with itself
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IOrgProfileRepository>();

        var org = Tenant.CreateOrganization("usernamecheck-c@example.com", 25);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var profile = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "my-slug", null, null, null);
        await repository.AddAsync(profile, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act — checking availability while excluding this profile's own ID
        var available = await repository.IsUsernameAvailableAsync(org.Id, "my-slug", profile.Id, CancellationToken.None);

        // Assert — should be available since the only conflict is the profile itself
        available.Should().BeTrue();
    }

    [Fact]
    public async Task TwoDifferentUsersInSameOrg_WithDifferentUsernames_ShouldBothSucceed()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org = Tenant.CreateOrganization("twousers@example.com", 30);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var p1 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "user-one", null, null, null);
        var p2 = OrgProfile.Create(DatabaseSeeder.Tenant1Member.Id, org.Id, TenantKind.Organization, "user-two", null, null, null);
        dbContext.Set<OrgProfile>().AddRange(p1, p2);

        // Act + Assert — should not throw
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SameUserAcrossTwoOrgsWithSameUsername_ShouldBothSucceed()
    {
        // Arrange — a user may use the same slug in two different orgs
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org1 = Tenant.CreateOrganization("crossorg-same-slug-x@example.com", 31);
        var org2 = Tenant.CreateOrganization("crossorg-same-slug-y@example.com", 32);
        dbContext.Set<Tenant>().AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var p1 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org1.Id, TenantKind.Organization, "same-slug", null, null, null);
        var p2 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org2.Id, TenantKind.Organization, "same-slug", null, null, null);
        dbContext.Set<OrgProfile>().AddRange(p1, p2);

        // Act + Assert — uniqueness is scoped per-org, not globally
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Uniqueness constraints (database-level)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Uniqueness_SameUserSameOrg_ShouldThrow()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org = Tenant.CreateOrganization("uniqueuser@example.com", 40);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var p1 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "slug-one", null, null, null);
        dbContext.Set<OrgProfile>().Add(p1);
        await dbContext.SaveChangesAsync();

        // Second profile for same user+org violates (user_id, org_tenant_id) unique constraint.
        var p2 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "slug-two", null, null, null);
        dbContext.Set<OrgProfile>().Add(p2);

        // Act + Assert
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Uniqueness_SameOrgSameUsername_ShouldThrow()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org = Tenant.CreateOrganization("uniqueusername@example.com", 41);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var p1 = OrgProfile.Create(DatabaseSeeder.Tenant1Owner.Id, org.Id, TenantKind.Organization, "shared-slug", null, null, null);
        dbContext.Set<OrgProfile>().Add(p1);
        await dbContext.SaveChangesAsync();

        // Second profile for a different user in the same org with the same slug
        // violates (org_tenant_id, username) unique constraint.
        var p2 = OrgProfile.Create(DatabaseSeeder.Tenant1Member.Id, org.Id, TenantKind.Organization, "shared-slug", null, null, null);
        dbContext.Set<OrgProfile>().Add(p2);

        // Act + Assert
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}

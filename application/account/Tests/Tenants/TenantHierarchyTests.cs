using Account.Database;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Tenants;

/// <summary>
///     Covers the tenant hierarchy feature (Organizations and Teams) introduced in task f1-tenant-hierarchy.
///     Tests fall into two categories:
///     <list type="number">
///         <item>Domain invariants — pure in-memory assertions, no database round-trip.</item>
///         <item>Repository queries — save via EF, assert via <see cref="ITenantRepository" />.</item>
///     </list>
/// </summary>
public sealed class TenantHierarchyTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // Domain invariants (no database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateOrganization_ShouldHaveOrganizationKindAndNoParent()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);

        org.Kind.Should().Be(TenantKind.Organization);
        org.ParentTenantId.Should().BeNull();
    }

    [Fact]
    public void Create_ExistingFactory_ShouldProduceSoloTenantWithNoParent()
    {
        var solo = Tenant.Create("owner@test.com", 0);

        solo.Kind.Should().Be(TenantKind.Solo);
        solo.ParentTenantId.Should().BeNull();
    }

    [Fact]
    public void CreateTeam_WithOrganizationParent_ShouldHaveTeamKindAndCorrectParent()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);

        var team = Tenant.CreateTeam(org, 1);

        team.Kind.Should().Be(TenantKind.Team);
        team.ParentTenantId.Should().Be(org.Id);
    }

    [Fact]
    public void CreateTeam_WhenParentIsSolo_ShouldThrowInvalidOperationException()
    {
        var solo = Tenant.Create("owner@test.com", 0);

        var act = () => Tenant.CreateTeam(solo, 0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Organization*");
    }

    [Fact]
    public void CreateTeam_WhenParentIsTeam_ShouldThrowInvalidOperationException()
    {
        var org = Tenant.CreateOrganization("admin@acme.com", 0);
        var team = Tenant.CreateTeam(org, 1);

        // v1 restriction: no nesting beyond one level
        var act = () => Tenant.CreateTeam(team, 2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Organization*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Repository queries (database round-trip)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChildrenOfAsync_WithTwoTeamsUnderOrg_ShouldReturnBothTeams()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var org = Tenant.CreateOrganization("admin@acme.com", 1);
        var team1 = Tenant.CreateTeam(org, 2);
        var team2 = Tenant.CreateTeam(org, 3);
        dbContext.Set<Tenant>().AddRange(org, team1, team2);
        await dbContext.SaveChangesAsync();

        // Act
        var children = await repository.GetChildrenOfAsync(org.Id, CancellationToken.None);

        // Assert
        children.Should().HaveCount(2);
        children.Select(t => t.Id).Should().Contain([team1.Id, team2.Id]);
        children.Should().OnlyContain(t => t.Kind == TenantKind.Team);
    }

    [Fact]
    public async Task GetChildrenOfAsync_WhenChildIsSoftDeleted_ShouldExcludeDeletedChild()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var org = Tenant.CreateOrganization("admin@acme.com", 1);
        var activeTeam = Tenant.CreateTeam(org, 2);
        var deletedTeam = Tenant.CreateTeam(org, 3);
        dbContext.Set<Tenant>().AddRange(org, activeTeam, deletedTeam);
        await dbContext.SaveChangesAsync();

        Connection.Update("tenants", "id", deletedTeam.Id.Value, [("deleted_at", TimeProvider.GetUtcNow())]);

        // Act
        var children = await repository.GetChildrenOfAsync(org.Id, CancellationToken.None);

        // Assert
        children.Should().ContainSingle(t => t.Id == activeTeam.Id);
    }

    [Fact]
    public async Task GetParentOfAsync_WithTeamThatHasParent_ShouldReturnOrg()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var org = Tenant.CreateOrganization("admin@acme.com", 1);
        var team = Tenant.CreateTeam(org, 2);
        dbContext.Set<Tenant>().AddRange(org, team);
        await dbContext.SaveChangesAsync();

        // Act
        var parent = await repository.GetParentOfAsync(team.Id, CancellationToken.None);

        // Assert
        parent.Should().NotBeNull();
        parent!.Id.Should().Be(org.Id);
        parent.Kind.Should().Be(TenantKind.Organization);
    }

    [Fact]
    public async Task GetParentOfAsync_WithSoloTenant_ShouldReturnNull()
    {
        // The DatabaseSeeder already creates Tenant1 as a Solo tenant via Tenant.Create(...)
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var parent = await repository.GetParentOfAsync(DatabaseSeeder.Tenant1.Id, CancellationToken.None);

        parent.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingSoloTenant_ShouldLoadWithSoloKindAndNoParent()
    {
        // Backward-compat: tenants created before the hierarchy feature must load as Solo.
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

        var tenant = await repository.GetByIdAsync(DatabaseSeeder.Tenant1.Id, CancellationToken.None);

        tenant.Should().NotBeNull();
        tenant!.Kind.Should().Be(TenantKind.Solo);
        tenant.ParentTenantId.Should().BeNull();
    }
}

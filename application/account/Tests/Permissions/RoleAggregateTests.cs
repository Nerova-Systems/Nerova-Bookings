using Account.Database;
using Account.Features.Permissions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Account.Tests.Permissions;

/// <summary>
///     Covers the <see cref="Role" /> aggregate and supporting types introduced in task <c>f1-pbac-domain</c>.
///     Tests fall into three categories:
///     <list type="number">
///         <item>Domain invariants — pure in-memory assertions, no database round-trip.</item>
///         <item>Repository round-trips — save via EF Core, reload and assert correctness.</item>
///         <item>Database constraint enforcement — uniqueness and FK rules.</item>
///     </list>
/// </summary>
public sealed class RoleAggregateTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // Permission value type
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Permission_ToString_ShouldProduceLowerCamelDotLowercase()
    {
        new Permission(PermissionResource.EventType, PermissionAction.Create).ToString()
            .Should().Be("eventType.create");

        new Permission(PermissionResource.AuditLog, PermissionAction.Manage).ToString()
            .Should().Be("auditLog.manage");

        new Permission(PermissionResource.Billing, PermissionAction.Delete).ToString()
            .Should().Be("billing.delete");

        new Permission(PermissionResource.Sso, PermissionAction.Read).ToString()
            .Should().Be("sso.read");
    }

    [Theory]
    [InlineData("eventType.create", PermissionResource.EventType, PermissionAction.Create)]
    [InlineData("auditLog.manage", PermissionResource.AuditLog, PermissionAction.Manage)]
    [InlineData("billing.delete", PermissionResource.Billing, PermissionAction.Delete)]
    [InlineData("sso.read", PermissionResource.Sso, PermissionAction.Read)]
    public void Permission_Parse_ShouldRoundTrip(string input, PermissionResource resource, PermissionAction action)
    {
        var result = Permission.Parse(input);

        result.Resource.Should().Be(resource);
        result.Action.Should().Be(action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("noDotsHere")]
    [InlineData(".create")]
    [InlineData("eventType.")]
    [InlineData("eventType.unknownAction")]
    [InlineData("unknownResource.create")]
    public void Permission_TryParse_WhenInvalid_ShouldReturnFalse(string input)
    {
        Permission.TryParse(input, out _).Should().BeFalse();
    }

    [Fact]
    public void Permission_Parse_WhenInvalid_ShouldThrowFormatException()
    {
        var act = () => Permission.Parse("not.valid.format");

        // "not" is not a valid resource name
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Permission_All_ShouldContainAllResourceActionCombinations()
    {
        var resourceCount = Enum.GetValues<PermissionResource>().Length;
        var actionCount = Enum.GetValues<PermissionAction>().Length;

        Permission.All.Should().HaveCount(resourceCount * actionCount);
    }

    [Fact]
    public void Permission_Equality_ShouldBeValueBased()
    {
        var p1 = new Permission(PermissionResource.Booking, PermissionAction.Create);
        var p2 = new Permission(PermissionResource.Booking, PermissionAction.Create);
        var p3 = new Permission(PermissionResource.Booking, PermissionAction.Read);

        p1.Should().Be(p2);
        p1.Should().NotBe(p3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Role.CreateSystem factory
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateSystem_ShouldHaveNullTenantIdAndIsSystemTrue()
    {
        var role = SystemRoles.CreateOwnerRole();

        role.TenantId.Should().BeNull();
        role.IsSystem.Should().BeTrue();
        role.Name.Should().Be("Owner");
    }

    [Fact]
    public void CreateSystem_WhenNameIsEmpty_ShouldThrow()
    {
        var act = () => Role.CreateSystem(RoleId.NewId(), "", null, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateSystem_ShouldAssignAllOwnerPermissions()
    {
        var role = SystemRoles.CreateOwnerRole();

        role.Permissions.Should().HaveCount(Permission.All.Count);
        role.Permissions.Should().BeEquivalentTo(Permission.All);
    }

    [Fact]
    public void CreateSystem_AdminRole_ShouldExcludeBillingManageAndOrgDelete()
    {
        var role = SystemRoles.CreateAdminRole();

        role.Permissions.Should().NotContain(new Permission(PermissionResource.Billing, PermissionAction.Manage));
        role.Permissions.Should().NotContain(new Permission(PermissionResource.Organization, PermissionAction.Delete));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Role.CreateCustom factory
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateCustom_ForTeamTenant_ShouldSucceed()
    {
        var teamTenantId = DatabaseSeeder.Tenant1.Id;

        var role = Role.CreateCustom(teamTenantId, TenantKind.Team, "Editor", null);

        role.TenantId.Should().Be(teamTenantId);
        role.IsSystem.Should().BeFalse();
        role.Name.Should().Be("Editor");
        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void CreateCustom_ForOrganizationTenant_ShouldSucceed()
    {
        var orgTenantId = DatabaseSeeder.Tenant1.Id;

        var role = Role.CreateCustom(orgTenantId, TenantKind.Organization, "Moderator", "Custom org role");

        role.TenantId.Should().Be(orgTenantId);
        role.Name.Should().Be("Moderator");
        role.Description.Should().Be("Custom org role");
    }

    [Fact]
    public void CreateCustom_ForSoloTenant_ShouldThrowInvalidOperationException()
    {
        var soloTenantId = DatabaseSeeder.Tenant1.Id;

        var act = () => Role.CreateCustom(soloTenantId, TenantKind.Solo, "Editor", null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Solo*");
    }

    [Fact]
    public void CreateCustom_WhenNameIsEmpty_ShouldThrow()
    {
        var act = () => Role.CreateCustom(DatabaseSeeder.Tenant1.Id, TenantKind.Team, "", null);

        act.Should().Throw<ArgumentException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Role mutation — Grant / Revoke / Rename
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Grant_OnCustomRole_ShouldAddPermission()
    {
        var role = Role.CreateCustom(DatabaseSeeder.Tenant1.Id, TenantKind.Team, "Editor", null);
        var permission = new Permission(PermissionResource.Booking, PermissionAction.Read);

        role.Grant(permission);

        role.Permissions.Should().Contain(permission);
    }

    [Fact]
    public void Grant_DuplicatePermission_ShouldNotDuplicate()
    {
        var role = Role.CreateCustom(DatabaseSeeder.Tenant1.Id, TenantKind.Team, "Editor", null);
        var permission = new Permission(PermissionResource.Booking, PermissionAction.Read);

        role.Grant(permission);
        role.Grant(permission);

        role.Permissions.Should().ContainSingle(p => p == permission);
    }

    [Fact]
    public void Grant_OnSystemRole_ShouldThrowInvalidOperationException()
    {
        var systemRole = SystemRoles.CreateOwnerRole();

        var act = () => systemRole.Grant(new Permission(PermissionResource.Booking, PermissionAction.Read));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*system roles*");
    }

    [Fact]
    public void Revoke_OnCustomRole_ShouldRemovePermission()
    {
        var permission = new Permission(PermissionResource.Booking, PermissionAction.Read);
        var role = Role.CreateCustom(DatabaseSeeder.Tenant1.Id, TenantKind.Team, "Editor", null, [permission]);

        role.Revoke(permission);

        role.Permissions.Should().NotContain(permission);
    }

    [Fact]
    public void Revoke_NonExistentPermission_ShouldBeNoOp()
    {
        var role = Role.CreateCustom(DatabaseSeeder.Tenant1.Id, TenantKind.Team, "Editor", null);
        var permission = new Permission(PermissionResource.Booking, PermissionAction.Read);

        // Should not throw
        var act = () => role.Revoke(permission);
        act.Should().NotThrow();
    }

    [Fact]
    public void Revoke_OnSystemRole_ShouldThrowInvalidOperationException()
    {
        var systemRole = SystemRoles.CreateOwnerRole();
        var permission = Permission.All.First();

        var act = () => systemRole.Revoke(permission);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*system roles*");
    }

    [Fact]
    public void Rename_OnCustomRole_ShouldUpdateNameAndDescription()
    {
        var role = Role.CreateCustom(DatabaseSeeder.Tenant1.Id, TenantKind.Team, "OldName", "old desc");

        role.Rename("NewName", "new desc");

        role.Name.Should().Be("NewName");
        role.Description.Should().Be("new desc");
    }

    [Fact]
    public void Rename_OnSystemRole_ShouldThrowInvalidOperationException()
    {
        var systemRole = SystemRoles.CreateAdminRole();

        var act = () => systemRole.Rename("NewName", null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*system roles*");
    }

    [Fact]
    public void Rename_WhenNameIsEmpty_ShouldThrow()
    {
        var role = Role.CreateCustom(DatabaseSeeder.Tenant1.Id, TenantKind.Team, "Editor", null);

        var act = () => role.Rename("", null);

        act.Should().Throw<ArgumentException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SystemRoles constants
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SystemRoles_Ids_ShouldHaveRolPrefix()
    {
        SystemRoles.OwnerId.Value.Should().StartWith("rol_");
        SystemRoles.AdminId.Value.Should().StartWith("rol_");
        SystemRoles.MemberId.Value.Should().StartWith("rol_");
    }

    [Fact]
    public void SystemRoles_Ids_ShouldBeDeterminsticAndDistinct()
    {
        var ids = new[] { SystemRoles.OwnerId, SystemRoles.AdminId, SystemRoles.MemberId };

        ids.Should().OnlyHaveUniqueItems();
        SystemRoles.OwnerId.Should().Be(new RoleId("rol_00000000000000000000000001"));
        SystemRoles.AdminId.Should().Be(new RoleId("rol_00000000000000000000000002"));
        SystemRoles.MemberId.Should().Be(new RoleId("rol_00000000000000000000000003"));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Repository round-trip (database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SystemRoles_ShouldBeSeedInDatabase()
    {
        // DatabaseSeeder adds system roles in every test run
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var systemRoles = await repository.GetSystemRolesAsync(CancellationToken.None);

        systemRoles.Should().HaveCount(3);
        systemRoles.Should().Contain(r => r.Id == SystemRoles.OwnerId && r.Name == "Owner");
        systemRoles.Should().Contain(r => r.Id == SystemRoles.AdminId && r.Name == "Admin");
        systemRoles.Should().Contain(r => r.Id == SystemRoles.MemberId && r.Name == "Member");
    }

    [Fact]
    public async Task OwnerRole_ShouldHaveAllPermissions()
    {
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var ownerRole = await repository.GetByIdAsync(SystemRoles.OwnerId, CancellationToken.None);

        ownerRole.Should().NotBeNull();
        ownerRole!.Permissions.Should().HaveCount(Permission.All.Count);
    }

    [Fact]
    public async Task CustomRole_RoundTrip_ShouldPersistPermissionsCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var org = Tenant.CreateOrganization("custompermissions@example.com", 50);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var role = Role.CreateCustom(
            org.Id,
            TenantKind.Organization,
            "Editor",
            "Can edit bookings and event types",
            [
                new Permission(PermissionResource.Booking, PermissionAction.Read),
                new Permission(PermissionResource.Booking, PermissionAction.Update),
                new Permission(PermissionResource.EventType, PermissionAction.Read)
            ]
        );
        await repository.AddAsync(role, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act — reload from DB
        var loaded = await repository.GetByIdAsync(role.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Editor");
        loaded.TenantId.Should().Be(org.Id);
        loaded.IsSystem.Should().BeFalse();
        loaded.Permissions.Should().HaveCount(3);
        loaded.Permissions.Should().Contain(new Permission(PermissionResource.Booking, PermissionAction.Read));
        loaded.Permissions.Should().Contain(new Permission(PermissionResource.Booking, PermissionAction.Update));
        loaded.Permissions.Should().Contain(new Permission(PermissionResource.EventType, PermissionAction.Read));
    }

    [Fact]
    public async Task GetCustomRolesForTenantAsync_ShouldReturnOnlyTenantRoles()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var org = Tenant.CreateOrganization("customrolesfororg@example.com", 51);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var roleA = Role.CreateCustom(org.Id, TenantKind.Organization, "RoleA", null);
        var roleB = Role.CreateCustom(org.Id, TenantKind.Organization, "RoleB", null);
        await repository.AddAsync(roleA, CancellationToken.None);
        await repository.AddAsync(roleB, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var customRoles = await repository.GetCustomRolesForTenantAsync(org.Id, CancellationToken.None);

        // Assert — only this org's custom roles, not system roles
        customRoles.Should().HaveCount(2);
        customRoles.Should().OnlyContain(r => r.TenantId == org.Id);
    }

    [Fact]
    public async Task GetByNameAsync_WhenExists_ShouldReturnRole()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var org = Tenant.CreateOrganization("getbyname@example.com", 52);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var role = Role.CreateCustom(org.Id, TenantKind.Organization, "Moderator", null);
        await repository.AddAsync(role, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var found = await repository.GetByNameAsync(org.Id, "Moderator", CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(role.Id);
    }

    [Fact]
    public async Task GetByNameAsync_WhenNotExists_ShouldReturnNull()
    {
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var result = await repository.GetByNameAsync(DatabaseSeeder.Tenant1.Id, "NoSuchRole", CancellationToken.None);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Database constraint enforcement
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Uniqueness_DuplicateCustomRoleName_SameTenant_ShouldThrow()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var org = Tenant.CreateOrganization("duprole@example.com", 60);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var r1 = Role.CreateCustom(org.Id, TenantKind.Organization, "Editor", null);
        await repository.AddAsync(r1, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var r2 = Role.CreateCustom(org.Id, TenantKind.Organization, "Editor", null);
        await repository.AddAsync(r2, CancellationToken.None);

        // Act + Assert
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Uniqueness_SameCustomRoleName_DifferentTenants_ShouldSucceed()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var org1 = Tenant.CreateOrganization("difftenantA@example.com", 61);
        var org2 = Tenant.CreateOrganization("difftenantB@example.com", 62);
        dbContext.Set<Tenant>().AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var r1 = Role.CreateCustom(org1.Id, TenantKind.Organization, "Editor", null);
        var r2 = Role.CreateCustom(org2.Id, TenantKind.Organization, "Editor", null);
        await repository.AddAsync(r1, CancellationToken.None);
        await repository.AddAsync(r2, CancellationToken.None);

        // Act + Assert — same name, different tenants should not conflict
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}

using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Services;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.Permissions;

/// <summary>
///     Integration tests for <see cref="PermissionCheckService" />.
///     Tests fall into four categories:
///     <list type="number">
///         <item>Team/Org membership — system role permission resolution.</item>
///         <item>Pending membership — should be treated as no access.</item>
///         <item>Custom role override — membership with CustomRoleId.</item>
///         <item>Solo tenant — permissions from User.Role, no Membership row.</item>
///         <item>Per-request caching — DB is queried only once per (userId, tenantId) pair.</item>
///         <item>Multi-tenant scoping — same user scoped correctly across two orgs.</item>
///     </list>
/// </summary>
public sealed class PermissionCheckServiceTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // Team / Org membership — system role resolution
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasPermissionAsync_WhenOwnerMembership_ShouldGrantAllPermissions()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var org = Tenant.CreateOrganization("owner@acme.com", 10);
        dbContext.Set<Tenant>().Add(org);
        var membership = Membership.CreateSeedOwner(org.Id, DatabaseSeeder.Tenant1Owner.Id);
        dbContext.Set<Membership>().Add(membership);
        await dbContext.SaveChangesAsync();

        // Act / Assert — owners hold every permission
        var samplePermissions = new[]
        {
            new Permission(PermissionResource.Billing, PermissionAction.Manage),
            new Permission(PermissionResource.Organization, PermissionAction.Delete),
            new Permission(PermissionResource.EventType, PermissionAction.Create)
        };

        foreach (var permission in samplePermissions)
        {
            (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, permission, CancellationToken.None)).Should().BeTrue(because: $"owner should have {permission}");
        }
    }

    [Fact]
    public async Task HasPermissionAsync_WhenAdminMembership_ShouldDenyBillingManageAndOrgDelete()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var org = Tenant.CreateOrganization("admin@acme.com", 10);
        dbContext.Set<Tenant>().Add(org);

        var membership = Membership.CreateInvite(org.Id, DatabaseSeeder.Tenant1Owner.Id, MembershipRole.Admin, DatabaseSeeder.Tenant1Member.Id, "token-admin-12345678901234567890123456789012345678901234");
        membership.Accept(DateTimeOffset.UtcNow);
        dbContext.Set<Membership>().Add(membership);
        await dbContext.SaveChangesAsync();

        // Act / Assert — admins lack two sensitive permissions
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Billing, PermissionAction.Manage), CancellationToken.None)).Should().BeFalse();
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Organization, PermissionAction.Delete), CancellationToken.None)).Should().BeFalse();

        // But admins can read team info
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Team, PermissionAction.Read), CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenMemberMembership_ShouldGrantMemberPermissionsOnly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var org = Tenant.CreateOrganization("member@acme.com", 10);
        dbContext.Set<Tenant>().Add(org);

        var membership = Membership.CreateInvite(org.Id, DatabaseSeeder.Tenant1Owner.Id, MembershipRole.Member, DatabaseSeeder.Tenant1Member.Id, "token-member-1234567890123456789012345678901234567890");
        membership.Accept(DateTimeOffset.UtcNow);
        dbContext.Set<Membership>().Add(membership);
        await dbContext.SaveChangesAsync();

        // Members have team.read
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Team, PermissionAction.Read), CancellationToken.None)).Should().BeTrue();

        // Members do NOT have team.create or billing.manage
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Team, PermissionAction.Create), CancellationToken.None)).Should().BeFalse();
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Billing, PermissionAction.Manage), CancellationToken.None)).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pending membership
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasPermissionAsync_WhenMembershipIsPending_ShouldDenyAll()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var org = Tenant.CreateOrganization("pending@acme.com", 10);
        dbContext.Set<Tenant>().Add(org);

        // Invite not yet accepted — Accepted == false
        var membership = Membership.CreateInvite(org.Id, DatabaseSeeder.Tenant1Owner.Id, MembershipRole.Owner, DatabaseSeeder.Tenant1Member.Id, "token-pending-123456789012345678901234567890123456789");
        dbContext.Set<Membership>().Add(membership);
        await dbContext.SaveChangesAsync();

        // Act / Assert
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Team, PermissionAction.Read), CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenNoMembership_ShouldDenyAll()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var org = Tenant.CreateOrganization("nomember@acme.com", 10);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        // No membership row at all
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Team, PermissionAction.Read), CancellationToken.None)).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Custom role override
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasPermissionAsync_WhenMemberHasCustomRole_ShouldUseCustomRolePermissions()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var org = Tenant.CreateOrganization("custom@acme.com", 10);
        dbContext.Set<Tenant>().Add(org);

        // Custom role grants only EventType.Create
        var customRole = Role.CreateCustom(org.Id, TenantKind.Organization, "Editor", null, [new Permission(PermissionResource.EventType, PermissionAction.Create)]);
        dbContext.Set<Role>().Add(customRole);

        var membership = Membership.CreateInvite(org.Id, DatabaseSeeder.Tenant1Owner.Id, MembershipRole.Member, DatabaseSeeder.Tenant1Member.Id, "token-custom-1234567890123456789012345678901234567890");
        membership.Accept(DateTimeOffset.UtcNow);
        membership.AssignCustomRole(customRole.Id);
        dbContext.Set<Membership>().Add(membership);
        await dbContext.SaveChangesAsync();

        // Custom role grants EventType.Create
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.EventType, PermissionAction.Create), CancellationToken.None)).Should().BeTrue();

        // Custom role does NOT grant Billing.Manage (even though Member system role wouldn't either)
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Billing, PermissionAction.Manage), CancellationToken.None)).Should().BeFalse();

        // Custom role does NOT grant Team.Read (which Member system role would have granted)
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, new Permission(PermissionResource.Team, PermissionAction.Read), CancellationToken.None)).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Solo tenant — User.Role path (no Membership row)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasPermissionAsync_WhenSoloTenantOwner_ShouldGrantOwnerPermissions()
    {
        // Arrange — DatabaseSeeder.Tenant1 is a Solo tenant; Tenant1Owner has UserRole.Owner
        using var scope = Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var soloTenantId = DatabaseSeeder.Tenant1.Id;
        var ownerId = DatabaseSeeder.Tenant1Owner.Id;

        // Owners can manage billing and delete the org
        (await service.HasPermissionAsync(ownerId, soloTenantId, new Permission(PermissionResource.Billing, PermissionAction.Manage), CancellationToken.None)).Should().BeTrue();
        (await service.HasPermissionAsync(ownerId, soloTenantId, new Permission(PermissionResource.Organization, PermissionAction.Delete), CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenSoloTenantMember_ShouldGrantMemberPermissionsOnly()
    {
        // Arrange — DatabaseSeeder.Tenant1Member has UserRole.Member on the Solo tenant
        using var scope = Provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var soloTenantId = DatabaseSeeder.Tenant1.Id;
        var memberId = DatabaseSeeder.Tenant1Member.Id;

        // Members can read teams
        (await service.HasPermissionAsync(memberId, soloTenantId, new Permission(PermissionResource.Team, PermissionAction.Read), CancellationToken.None)).Should().BeTrue();

        // Members cannot manage billing
        (await service.HasPermissionAsync(memberId, soloTenantId, new Permission(PermissionResource.Billing, PermissionAction.Manage), CancellationToken.None)).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Per-request caching
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasPermissionAsync_WhenCalledMultipleTimes_ShouldOnlyQueryRepositoryOnce()
    {
        // Arrange — use NSubstitute mocks to count DB calls
        var membershipRepo = Substitute.For<IMembershipRepository>();
        var roleRepo = Substitute.For<IRoleRepository>();
        var tenantRepo = Substitute.For<ITenantRepository>();
        var userRepo = Substitute.For<IUserRepository>();

        var org = Tenant.CreateOrganization("cache@acme.com", 0);
        var userId = DatabaseSeeder.Tenant1Owner.Id;

        tenantRepo.GetByIdUnfilteredAsync(org.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Tenant?>(org));

        var membership = Membership.CreateSeedOwner(org.Id, userId);
        membershipRepo.GetByUserAndTenantAsync(userId, org.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Membership?>(membership));

        var service = new PermissionCheckService(membershipRepo, roleRepo, tenantRepo, userRepo);

        // Act — call three times for the same (userId, tenantId)
        var permission = new Permission(PermissionResource.Team, PermissionAction.Read);
        await service.HasPermissionAsync(userId, org.Id, permission, CancellationToken.None);
        await service.HasPermissionAsync(userId, org.Id, permission, CancellationToken.None);
        await service.HasPermissionAsync(userId, org.Id, permission, CancellationToken.None);

        // Assert — repositories called only once despite three HasPermissionAsync invocations
        await tenantRepo.Received(1).GetByIdUnfilteredAsync(org.Id, Arg.Any<CancellationToken>());
        await membershipRepo.Received(1).GetByUserAndTenantAsync(userId, org.Id, Arg.Any<CancellationToken>());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multi-tenant scoping
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HasPermissionAsync_WhenSameUserInTwoOrgs_ShouldScopePermissionsPerTenant()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPermissionCheckService>();

        var org1 = Tenant.CreateOrganization("org1@acme.com", 10);
        var org2 = Tenant.CreateOrganization("org2@acme.com", 11);
        dbContext.Set<Tenant>().AddRange(org1, org2);

        // Same user: Owner in org1, Member in org2
        var ownerMembership = Membership.CreateSeedOwner(org1.Id, DatabaseSeeder.Tenant1Owner.Id);
        var memberMembership = Membership.CreateInvite(org2.Id, DatabaseSeeder.Tenant1Owner.Id, MembershipRole.Member, DatabaseSeeder.Tenant1Member.Id, "token-scope-12345678901234567890123456789012345678901");
        memberMembership.Accept(DateTimeOffset.UtcNow);
        dbContext.Set<Membership>().AddRange(ownerMembership, memberMembership);
        await dbContext.SaveChangesAsync();

        var billingManage = new Permission(PermissionResource.Billing, PermissionAction.Manage);

        // In org1 (Owner) — billing.manage is granted
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org1.Id, billingManage, CancellationToken.None)).Should().BeTrue();

        // In org2 (Member) — billing.manage is denied
        (await service.HasPermissionAsync(DatabaseSeeder.Tenant1Owner.Id, org2.Id, billingManage, CancellationToken.None)).Should().BeFalse();
    }
}

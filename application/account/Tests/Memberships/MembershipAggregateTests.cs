using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Account.Tests.Memberships;

/// <summary>
///     Covers the <see cref="Membership" /> aggregate introduced in task <c>f1-membership</c>.
///     Tests fall into three categories:
///     <list type="number">
///         <item>Domain invariants — pure in-memory assertions, no database round-trip.</item>
///         <item>Repository queries — save via EF, assert via <see cref="IMembershipRepository" />.</item>
///         <item>Uniqueness constraint — database-level unique constraint enforcement.</item>
///     </list>
/// </summary>
public sealed class MembershipAggregateTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // Domain invariants (no database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateSeedOwner_ShouldBeAcceptedOwnerWithNoToken()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var user = DatabaseSeeder.Tenant1Owner;

        var membership = Membership.CreateSeedOwner(org.Id, user.Id);

        membership.TenantId.Should().Be(org.Id);
        membership.UserId.Should().Be(user.Id);
        membership.Role.Should().Be(MembershipRole.Owner);
        membership.Accepted.Should().BeTrue();
        membership.AcceptedAt.Should().NotBeNull();
        membership.InviteToken.Should().BeNull();
        membership.InvitedBy.Should().BeNull();
        membership.DisableImpersonation.Should().BeFalse();
    }

    [Fact]
    public void CreateInvite_ShouldBePendingWithToken()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var user = DatabaseSeeder.Tenant1Owner;
        var inviter = DatabaseSeeder.Tenant1Member;
        const string token = "abc123def456abc123def456abc123def456abc123def456abc123def456ab12";

        var membership = Membership.CreateInvite(org.Id, user.Id, MembershipRole.Member, inviter.Id, token);

        membership.TenantId.Should().Be(org.Id);
        membership.UserId.Should().Be(user.Id);
        membership.Role.Should().Be(MembershipRole.Member);
        membership.Accepted.Should().BeFalse();
        membership.AcceptedAt.Should().BeNull();
        membership.InviteToken.Should().Be(token);
        membership.InvitedBy.Should().Be(inviter.Id);
        membership.DisableImpersonation.Should().BeFalse();
    }

    [Fact]
    public void CreateInvite_WhenTokenIsEmpty_ShouldThrow()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var user = DatabaseSeeder.Tenant1Owner;
        var inviter = DatabaseSeeder.Tenant1Member;

        var act = () => Membership.CreateInvite(org.Id, user.Id, MembershipRole.Member, inviter.Id, "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Accept_WhenPending_ShouldTransitionToAccepted()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var user = DatabaseSeeder.Tenant1Owner;
        var inviter = DatabaseSeeder.Tenant1Member;
        const string token = "abc123def456abc123def456abc123def456abc123def456abc123def456ab12";
        var membership = Membership.CreateInvite(org.Id, user.Id, MembershipRole.Member, inviter.Id, token);

        var acceptedAt = DateTimeOffset.UtcNow;
        membership.Accept(acceptedAt);

        membership.Accepted.Should().BeTrue();
        membership.AcceptedAt.Should().Be(acceptedAt);
        membership.InviteToken.Should().BeNull();
    }

    [Fact]
    public void Accept_WhenAlreadyAccepted_ShouldThrowInvalidOperationException()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var user = DatabaseSeeder.Tenant1Owner;
        var seedOwner = Membership.CreateSeedOwner(org.Id, user.Id);

        var act = () => seedOwner.Accept(DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already accepted*");
    }

    [Fact]
    public void ChangeRole_ShouldUpdateRole()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var user = DatabaseSeeder.Tenant1Owner;
        var changedBy = DatabaseSeeder.Tenant1Member;
        var membership = Membership.CreateSeedOwner(org.Id, user.Id);

        membership.ChangeRole(MembershipRole.Admin, changedBy.Id);

        membership.Role.Should().Be(MembershipRole.Admin);
    }

    [Fact]
    public void SetDisableImpersonation_ShouldUpdateFlag()
    {
        var org = Tenant.CreateOrganization("owner@acme.com", 0);
        var membership = Membership.CreateSeedOwner(org.Id, DatabaseSeeder.Tenant1Owner.Id);

        membership.SetDisableImpersonation(true);

        membership.DisableImpersonation.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Repository round-trip (database)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedOwner_RoundTrip_ShouldPersistAndReloadCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org = Tenant.CreateOrganization("seed-roundtrip@example.com", 10);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var membership = Membership.CreateSeedOwner(org.Id, DatabaseSeeder.Tenant1Owner.Id);
        await repository.AddAsync(membership, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var loaded = await repository.GetByIdAsync(membership.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.TenantId.Should().Be(org.Id);
        loaded.UserId.Should().Be(DatabaseSeeder.Tenant1Owner.Id);
        loaded.Role.Should().Be(MembershipRole.Owner);
        loaded.Accepted.Should().BeTrue();
        loaded.AcceptedAt.Should().NotBeNull();
        loaded.InviteToken.Should().BeNull();
        loaded.InvitedBy.Should().BeNull();
        loaded.DisableImpersonation.Should().BeFalse();
    }

    [Fact]
    public async Task CreateInvite_RoundTrip_ShouldPersistAndReloadCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org = Tenant.CreateOrganization("invite-roundtrip@example.com", 11);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        const string token = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
        var membership = Membership.CreateInvite(
            org.Id,
            DatabaseSeeder.Tenant1Member.Id,
            MembershipRole.Admin,
            DatabaseSeeder.Tenant1Owner.Id,
            token
        );
        await repository.AddAsync(membership, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var loaded = await repository.GetByIdAsync(membership.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Role.Should().Be(MembershipRole.Admin);
        loaded.Accepted.Should().BeFalse();
        loaded.AcceptedAt.Should().BeNull();
        loaded.InviteToken.Should().Be(token);
        loaded.InvitedBy.Should().Be(DatabaseSeeder.Tenant1Owner.Id);
    }

    [Fact]
    public async Task GetByUserAndTenantAsync_WhenMemberExists_ShouldReturnMembership()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org = Tenant.CreateOrganization("byuserandtenant@example.com", 12);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var membership = Membership.CreateSeedOwner(org.Id, DatabaseSeeder.Tenant1Owner.Id);
        await repository.AddAsync(membership, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByUserAndTenantAsync(DatabaseSeeder.Tenant1Owner.Id, org.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(membership.Id);
    }

    [Fact]
    public async Task GetByUserAndTenantAsync_WhenNotAMember_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org = Tenant.CreateOrganization("notamember@example.com", 13);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        // Act — Tenant1Member is not a member of this org
        var result = await repository.GetByUserAndTenantAsync(DatabaseSeeder.Tenant1Member.Id, org.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByInviteTokenAsync_WhenTokenExists_ShouldReturnMembership()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org = Tenant.CreateOrganization("invitetoken@example.com", 14);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        const string token = "cafecafecafecafecafecafecafecafecafecafecafecafecafecafecafecafe";
        var membership = Membership.CreateInvite(
            org.Id,
            DatabaseSeeder.Tenant1Member.Id,
            MembershipRole.Member,
            DatabaseSeeder.Tenant1Owner.Id,
            token
        );
        await repository.AddAsync(membership, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByInviteTokenAsync(token, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(membership.Id);
    }

    [Fact]
    public async Task GetByInviteTokenAsync_WhenTokenNotFound_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        // Act
        var result = await repository.GetByInviteTokenAsync("nonexistenttokenvalue", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMembersOfTenantAsync_WhenIncludePendingTrue_ShouldReturnAllMembers()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org = Tenant.CreateOrganization("membersoforg@example.com", 15);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var ownerMembership = Membership.CreateSeedOwner(org.Id, DatabaseSeeder.Tenant1Owner.Id);
        const string pendingToken = "aabbccddeeffaabbccddeeffaabbccddeeffaabbccddeeffaabbccddeeffaabb";
        var pendingMembership = Membership.CreateInvite(
            org.Id,
            DatabaseSeeder.Tenant1Member.Id,
            MembershipRole.Member,
            DatabaseSeeder.Tenant1Owner.Id,
            pendingToken
        );
        dbContext.Set<Membership>().AddRange(ownerMembership, pendingMembership);
        await dbContext.SaveChangesAsync();

        // Act
        var all = await repository.GetMembersOfTenantAsync(org.Id, includePending: true, CancellationToken.None);
        var acceptedOnly = await repository.GetMembersOfTenantAsync(org.Id, includePending: false, CancellationToken.None);

        // Assert
        all.Should().HaveCount(2);
        acceptedOnly.Should().HaveCount(1);
        acceptedOnly.Single().Role.Should().Be(MembershipRole.Owner);
    }

    [Fact]
    public async Task GetMembershipsOfUserAsync_ShouldReturnAcrossAllTenants()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org1 = Tenant.CreateOrganization("crosstenantA@example.com", 20);
        var org2 = Tenant.CreateOrganization("crosstenantB@example.com", 21);
        dbContext.Set<Tenant>().AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var m1 = Membership.CreateSeedOwner(org1.Id, DatabaseSeeder.Tenant1Owner.Id);
        var m2 = Membership.CreateSeedOwner(org2.Id, DatabaseSeeder.Tenant1Owner.Id);
        dbContext.Set<Membership>().AddRange(m1, m2);
        await dbContext.SaveChangesAsync();

        // Act — query crosses both org tenants
        var memberships = await repository.GetMembershipsOfUserAsync(DatabaseSeeder.Tenant1Owner.Id, CancellationToken.None);

        // Assert
        memberships.Should().HaveCount(2);
        memberships.Select(m => m.TenantId).Should().BeEquivalentTo([org1.Id, org2.Id]);
    }

    [Fact]
    public async Task CountOwnersAsync_ShouldReturnCountOfOwnerMemberships()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();

        var org = Tenant.CreateOrganization("countowners@example.com", 22);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var ownerMembership = Membership.CreateSeedOwner(org.Id, DatabaseSeeder.Tenant1Owner.Id);
        const string token = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        var memberMembership = Membership.CreateInvite(
            org.Id,
            DatabaseSeeder.Tenant1Member.Id,
            MembershipRole.Member,
            DatabaseSeeder.Tenant1Owner.Id,
            token
        );
        dbContext.Set<Membership>().AddRange(ownerMembership, memberMembership);
        await dbContext.SaveChangesAsync();

        // Act
        var ownerCount = await repository.CountOwnersAsync(org.Id, CancellationToken.None);

        // Assert
        ownerCount.Should().Be(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Uniqueness constraint (database-level)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Uniqueness_SameUserSameTenant_ShouldThrow()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org = Tenant.CreateOrganization("duplicatemember@example.com", 30);
        dbContext.Set<Tenant>().Add(org);
        await dbContext.SaveChangesAsync();

        var m1 = Membership.CreateSeedOwner(org.Id, DatabaseSeeder.Tenant1Owner.Id);
        dbContext.Set<Membership>().Add(m1);
        await dbContext.SaveChangesAsync();

        // Second membership for same user+tenant should fail
        const string token = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        var m2 = Membership.CreateInvite(
            org.Id,
            DatabaseSeeder.Tenant1Owner.Id,
            MembershipRole.Member,
            DatabaseSeeder.Tenant1Member.Id,
            token
        );
        dbContext.Set<Membership>().Add(m2);

        // Act + Assert
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Uniqueness_SameUserDifferentTenants_ShouldSucceed()
    {
        // Arrange — a user CAN be a member of two separate orgs
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var org1 = Tenant.CreateOrganization("multiorg-x@example.com", 31);
        var org2 = Tenant.CreateOrganization("multiorg-y@example.com", 32);
        dbContext.Set<Tenant>().AddRange(org1, org2);
        await dbContext.SaveChangesAsync();

        var m1 = Membership.CreateSeedOwner(org1.Id, DatabaseSeeder.Tenant1Owner.Id);
        var m2 = Membership.CreateSeedOwner(org2.Id, DatabaseSeeder.Tenant1Owner.Id);
        dbContext.Set<Membership>().AddRange(m1, m2);

        // Act + Assert — should not throw
        var act = async () => await dbContext.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}

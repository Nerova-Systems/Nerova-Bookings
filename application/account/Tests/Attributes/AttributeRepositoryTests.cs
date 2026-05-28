using Account.Database;
using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using Xunit;
using OrgAttribute = Account.Features.Attributes.Domain.Attribute;

namespace Account.Tests.Attributes;

/// <summary>
///     Database round-trip tests for <see cref="IAttributeRepository" /> and
///     <see cref="IAttributeAssignmentRepository" />.
///     Uses an in-memory SQLite database via <see cref="AccountWebApplicationFactory" />.
/// </summary>
public sealed class AttributeRepositoryTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // IAttributeRepository — Add + GetByOrgUnfilteredAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenGetByOrgUnfilteredAsync_ShouldRoundTripAttribute()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IAttributeRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var orgId = await SeedOrgTenantAsync(db);

        var attribute = OrgAttribute.Create(orgId, TenantKind.Organization, "Department", AttributeType.Text);
        await repo.AddAsync(attribute, CancellationToken.None);
        await db.SaveChangesAsync();

        var list = await repo.GetByOrgUnfilteredAsync(orgId, CancellationToken.None);

        list.Should().ContainSingle(a => a.Id == attribute.Id);
        var loaded = list.Single(a => a.Id == attribute.Id);
        loaded.Name.Should().Be("Department");
        loaded.Type.Should().Be(AttributeType.Text);
        loaded.TenantId.Should().Be(orgId);
    }

    [Fact]
    public async Task GetByOrgUnfilteredAsync_ShouldNotReturnOtherOrgsAttributes()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IAttributeRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var org1 = await SeedOrgTenantAsync(db);
        var org2 = await SeedOrgTenantAsync(db);

        var attr1 = OrgAttribute.Create(org1, TenantKind.Organization, "Org1Attr", AttributeType.Text);
        var attr2 = OrgAttribute.Create(org2, TenantKind.Organization, "Org2Attr", AttributeType.Text);
        await repo.AddAsync(attr1, CancellationToken.None);
        await repo.AddAsync(attr2, CancellationToken.None);
        await db.SaveChangesAsync();

        var result = await repo.GetByOrgUnfilteredAsync(org1, CancellationToken.None);

        result.Select(a => a.Id).Should().Contain(attr1.Id);
        result.Select(a => a.Id).Should().NotContain(attr2.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IAttributeRepository — GetByIdUnfilteredAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdUnfilteredAsync_ShouldReturnAttributeWithOptions()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IAttributeRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var orgId = await SeedOrgTenantAsync(db);

        var attribute = OrgAttribute.Create(orgId, TenantKind.Organization, "Role", AttributeType.SingleSelect);
        attribute.AddOption("Engineer");
        attribute.AddOption("Designer");
        await repo.AddAsync(attribute, CancellationToken.None);
        await db.SaveChangesAsync();

        var loaded = await repo.GetByIdUnfilteredAsync(attribute.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded.Options.Should().HaveCount(2);
        loaded.Options.Should().Contain(o => o.Value == "Engineer");
        loaded.Options.Should().Contain(o => o.Value == "Designer");
    }

    [Fact]
    public async Task GetByIdUnfilteredAsync_WhenNotFound_ShouldReturnNull()
    {
        using var scope = Provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAttributeRepository>();

        var result = await repo.GetByIdUnfilteredAsync(AttributeId.NewId(), CancellationToken.None);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IAttributeRepository — SlugExistsUnfilteredAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SlugExistsUnfilteredAsync_WhenSlugPresent_ShouldReturnTrue()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IAttributeRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var orgId = await SeedOrgTenantAsync(db);

        var attribute = OrgAttribute.Create(orgId, TenantKind.Organization, "My Field", AttributeType.Text);
        await repo.AddAsync(attribute, CancellationToken.None);
        await db.SaveChangesAsync();

        var exists = await repo.SlugExistsUnfilteredAsync(orgId, "my-field", CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task SlugExistsUnfilteredAsync_WhenSlugAbsent_ShouldReturnFalse()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IAttributeRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var orgId = await SeedOrgTenantAsync(db);

        var exists = await repo.SlugExistsUnfilteredAsync(orgId, "no-such-slug", CancellationToken.None);

        exists.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IAttributeAssignmentRepository — Add + GetByMembershipAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAssignment_ThenGetByMembershipAsync_ShouldRoundTrip()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();

        var orgId = await SeedOrgTenantAsync(db);
        var membershipId = await SeedMembershipAsync(db, orgId);

        var attribute = OrgAttribute.Create(orgId, TenantKind.Organization, "Level", AttributeType.Text);
        await attrRepo.AddAsync(attribute, CancellationToken.None);
        await db.SaveChangesAsync();

        var assignment = AttributeAssignment.Create(orgId, membershipId, attribute.Id, null, "Senior", null);
        await assignRepo.AddAsync(assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        var list = await assignRepo.GetByMembershipAsync(membershipId, CancellationToken.None);

        list.Should().ContainSingle(a => a.Id == assignment.Id);
        var loaded = list.Single(a => a.Id == assignment.Id);
        loaded.Value.Should().Be("Senior");
        loaded.AttributeId.Should().Be(attribute.Id);
    }

    [Fact]
    public async Task GetByMembershipAttributeOptionAsync_WhenExists_ShouldReturnAssignment()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();

        var orgId = await SeedOrgTenantAsync(db);
        var membershipId = await SeedMembershipAsync(db, orgId);

        var attribute = OrgAttribute.Create(orgId, TenantKind.Organization, "Role", AttributeType.SingleSelect);
        var option = attribute.AddOption("Engineer");
        await attrRepo.AddAsync(attribute, CancellationToken.None);
        await db.SaveChangesAsync();

        var assignment = AttributeAssignment.Create(orgId, membershipId, attribute.Id, option.Id, null, null);
        await assignRepo.AddAsync(assignment, CancellationToken.None);
        await db.SaveChangesAsync();

        var found = await assignRepo.GetByMembershipAttributeOptionAsync(
            membershipId, attribute.Id, option.Id, CancellationToken.None
        );

        found.Should().NotBeNull();
        found.AttributeOptionId.Should().Be(option.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<TenantId> SeedOrgTenantAsync(AccountDbContext db)
    {
        var org = Tenant.CreateOrganization(Faker.Internet.Email(), 0);
        db.Set<Tenant>().Add(org);
        db.Set<Subscription>().Add(Subscription.Create(org.Id));
        await db.SaveChangesAsync();
        return org.Id;
    }

    private async Task<MembershipId> SeedMembershipAsync(AccountDbContext db, TenantId orgId)
    {
        var userTenant = Tenant.Create(Faker.Internet.Email(), 0);
        db.Set<Tenant>().Add(userTenant);
        db.Set<Subscription>().Add(Subscription.Create(userTenant.Id));
        var user = User.Create(userTenant.Id, Faker.Internet.Email(), UserRole.Member, true, null, 0);
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        var membership = Membership.CreateSeedOwner(orgId, user.Id);
        db.Set<Membership>().Add(membership);
        await db.SaveChangesAsync();
        return membership.Id;
    }
}

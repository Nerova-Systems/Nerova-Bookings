using System.Security.Cryptography;
using System.Text;
using Account.Database;
using Account.Features.ApiKeys.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.ApiKeys;

/// <summary>
///     Database round-trip tests for <see cref="IApiKeyRepository" />.
///     Uses an in-memory SQLite database via <see cref="AccountWebApplicationFactory" />.
/// </summary>
public sealed class ApiKeyRepositoryTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // AddAsync + GetByHashAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenGetByHashAsync_ShouldRoundTripKey()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IApiKeyRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var (userId, userTenantId) = await SeedUserAsync(db);

        var (key, plainText) = ApiKey.CreateUserKey(userTenantId, userId, "Test Key", null);
        await repo.AddAsync(key, CancellationToken.None);
        await db.SaveChangesAsync();

        // Act
        var hash = ComputeHash(plainText);
        var loaded = await repo.GetByHashAsync(hash, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded.Id.Should().Be(key.Id);
        loaded.Scope.Should().Be(ApiKeyScope.User);
        loaded.TenantId.Should().Be(userTenantId);
        loaded.KeyPrefix.Should().Be(key.KeyPrefix);
        loaded.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetByHashAsync_WhenHashDoesNotExist_ShouldReturnNull()
    {
        using var scope = Provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var result = await repo.GetByHashAsync("0000000000000000000000000000000000000000000000000000000000000000", CancellationToken.None);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetByUserAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserAsync_ShouldReturnAllKeysForTenant()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IApiKeyRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var (userId, tenantId) = await SeedUserAsync(db);

        var (key1, _) = ApiKey.CreateUserKey(tenantId, userId, "Key 1", null);
        var (key2, _) = ApiKey.CreateUserKey(tenantId, userId, "Key 2", null);
        await repo.AddAsync(key1, CancellationToken.None);
        await repo.AddAsync(key2, CancellationToken.None);
        await db.SaveChangesAsync();

        var keys = await repo.GetByUserAsync(tenantId, CancellationToken.None);

        keys.Should().HaveCountGreaterThanOrEqualTo(2);
        keys.Select(k => k.Id).Should().Contain([key1.Id, key2.Id]);
    }

    [Fact]
    public async Task GetByUserAsync_ShouldNotReturnOtherTenantsKeys()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IApiKeyRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();

        var (userId1, tenantId1) = await SeedUserAsync(db);
        var (userId2, tenantId2) = await SeedUserAsync(db);

        var (key1, _) = ApiKey.CreateUserKey(tenantId1, userId1, "Tenant1 Key", null);
        var (key2, _) = ApiKey.CreateUserKey(tenantId2, userId2, "Tenant2 Key", null);
        await repo.AddAsync(key1, CancellationToken.None);
        await repo.AddAsync(key2, CancellationToken.None);
        await db.SaveChangesAsync();

        var result = await repo.GetByUserAsync(tenantId1, CancellationToken.None);

        result.Select(k => k.Id).Should().Contain(key1.Id);
        result.Select(k => k.Id).Should().NotContain(key2.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetByOrgAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByOrgAsync_ShouldReturnAllOrgScopeKeysForOrg()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IApiKeyRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();

        var (userId, _) = await SeedUserAsync(db);
        var orgId = await SeedOrgTenantAsync(db);

        var (orgKey, _) = ApiKey.CreateOrgKey(orgId, userId, "Org Key", null);
        await repo.AddAsync(orgKey, CancellationToken.None);
        await db.SaveChangesAsync();

        var result = await repo.GetByOrgAsync(orgId, CancellationToken.None);

        result.Should().ContainSingle(k => k.Id == orgKey.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetByIdUnfilteredAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdUnfilteredAsync_ShouldReturnKeyRegardlessOfTenant()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repo = sp.GetRequiredService<IApiKeyRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();

        var (userId, _) = await SeedUserAsync(db);
        var orgId = await SeedOrgTenantAsync(db);

        var (orgKey, _) = ApiKey.CreateOrgKey(orgId, userId, "Cross-tenant Key", null);
        await repo.AddAsync(orgKey, CancellationToken.None);
        await db.SaveChangesAsync();

        var loaded = await repo.GetByIdUnfilteredAsync(orgKey.Id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded.Id.Should().Be(orgKey.Id);
        loaded.TenantId.Should().Be(orgId);
    }

    [Fact]
    public async Task GetByIdUnfilteredAsync_WhenNotFound_ShouldReturnNull()
    {
        using var scope = Provider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

        var result = await repo.GetByIdUnfilteredAsync(ApiKeyId.NewId(), CancellationToken.None);

        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(UserId UserId, TenantId TenantId)> SeedUserAsync(AccountDbContext db)
    {
        var tenant = SeedTenant(db, TenantKind.Solo);
        var user = User.Create(tenant.Id, Faker.Internet.Email(), UserRole.Owner, true, null, 0);
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return (user.Id, tenant.Id);
    }

    private async Task<TenantId> SeedOrgTenantAsync(AccountDbContext db)
    {
        var org = SeedTenant(db, TenantKind.Organization);
        await db.SaveChangesAsync();
        return org.Id;
    }

    private Tenant SeedTenant(AccountDbContext db, TenantKind kind)
    {
        var email = Faker.Internet.Email();
        var tenant = kind == TenantKind.Organization
            ? Tenant.CreateOrganization(email, 0)
            : Tenant.Create(email, 0);
        db.Set<Tenant>().Add(tenant);
        db.Set<Subscription>().Add(Subscription.Create(tenant.Id));
        return tenant;
    }

    private static string ComputeHash(string plainText)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

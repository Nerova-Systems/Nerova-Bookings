using Account.Database;
using Account.Features.DelegationCredentials.Domain;
using Account.Features.DelegationCredentials.Infrastructure;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.DelegationCredentials;

/// <summary>
///     Database round-trip tests for <see cref="IDelegationCredentialRepository" />.
///     <para>
///         All operations use the base <see cref="Provider" /> (no HTTP context → execution-context
///         TenantId is null). Both repository queries use <c>IgnoreQueryFilters(Tenant)</c>, so no
///         tenant-scoped provider is needed.
///     </para>
/// </summary>
public sealed class DelegationCredentialRepositoryTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private static readonly UserId SomeUserId = UserId.NewId();

    // ──────────────────────────────────────────────────────────────────────────
    // AddAsync + GetByIdAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ShouldRoundTripCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDelegationCredentialRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var credential = DelegationCredential.Create(
            orgTenant,
            WorkspacePlatform.Google,
            domain: "acme.com",
            encryptedKeyBlob: "enc_blob",
            createdByUserId: SomeUserId);

        // Act
        await repository.AddAsync(credential, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(credential.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(credential.Id);
        loaded.TenantId.Should().Be(orgTenant.Id);
        loaded.Platform.Should().Be(WorkspacePlatform.Google);
        loaded.Domain.Should().Be("acme.com");
        loaded.EncryptedKeyBlob.Should().Be("enc_blob");
        loaded.Status.Should().Be(DelegationCredentialStatus.Active);
        loaded.CreatedByUserId.Should().Be(SomeUserId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetByOrgAndPlatformAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByOrgAndPlatformAsync_WhenCredentialExists_ShouldReturnIt()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDelegationCredentialRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var credential = DelegationCredential.Create(orgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        await repository.AddAsync(credential, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByOrgAndPlatformAsync(orgTenant.Id, WorkspacePlatform.Google, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(credential.Id);
    }

    [Fact]
    public async Task GetByOrgAndPlatformAsync_WhenNoCredentialExists_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDelegationCredentialRepository>();

        // Act
        var result = await repository.GetByOrgAndPlatformAsync(TenantId.NewId(), WorkspacePlatform.Google, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByOrgAndPlatformAsync_WhenDifferentPlatformExists_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDelegationCredentialRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var credential = DelegationCredential.Create(orgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        await repository.AddAsync(credential, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act — query for Microsoft while only Google was stored
        var result = await repository.GetByOrgAndPlatformAsync(orgTenant.Id, WorkspacePlatform.Microsoft, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetAllByOrgIdAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllByOrgIdAsync_ShouldReturnAllCredentialsForOrg()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDelegationCredentialRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var google = DelegationCredential.Create(orgTenant, WorkspacePlatform.Google, "acme.com", "enc_g", SomeUserId);
        var microsoft = DelegationCredential.Create(orgTenant, WorkspacePlatform.Microsoft, "acme.com", "enc_m", SomeUserId);
        await repository.AddAsync(google, CancellationToken.None);
        await repository.AddAsync(microsoft, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var results = await repository.GetAllByOrgIdAsync(orgTenant.Id, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results.Select(c => c.Platform).Should().Contain([WorkspacePlatform.Google, WorkspacePlatform.Microsoft]);
    }

    [Fact]
    public async Task GetAllByOrgIdAsync_WithMultipleOrgs_ShouldOnlyReturnMatchingOrg()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDelegationCredentialRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();

        var orgA = await SeedOrgTenantAsync(dbContext);
        var orgB = await SeedOrgTenantAsync(dbContext);

        var credA = DelegationCredential.Create(orgA, WorkspacePlatform.Google, "a.com", "enc_a", SomeUserId);
        var credB = DelegationCredential.Create(orgB, WorkspacePlatform.Google, "b.com", "enc_b", SomeUserId);
        await repository.AddAsync(credA, CancellationToken.None);
        await repository.AddAsync(credB, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var resultsA = await repository.GetAllByOrgIdAsync(orgA.Id, CancellationToken.None);
        var resultsB = await repository.GetAllByOrgIdAsync(orgB.Id, CancellationToken.None);

        // Assert
        resultsA.Should().ContainSingle(c => c.Id == credA.Id);
        resultsA.Select(c => c.Id).Should().NotContain(credB.Id);
        resultsB.Should().ContainSingle(c => c.Id == credB.Id);
    }

    [Fact]
    public async Task GetAllByOrgIdAsync_WhenNoCredentialsExist_ShouldReturnEmpty()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDelegationCredentialRepository>();

        // Act
        var results = await repository.GetAllByOrgIdAsync(TenantId.NewId(), CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ThenGetByOrgAndPlatform_ShouldReturnUpdatedValues()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDelegationCredentialRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var credential = DelegationCredential.Create(orgTenant, WorkspacePlatform.Google, "old.com", "old_enc", SomeUserId);
        await repository.AddAsync(credential, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        credential.RotateKey("new_enc", "new.com");
        repository.Update(credential);
        await dbContext.SaveChangesAsync();

        var reloaded = await repository.GetByOrgAndPlatformAsync(orgTenant.Id, WorkspacePlatform.Google, CancellationToken.None);

        // Assert
        reloaded!.EncryptedKeyBlob.Should().Be("new_enc");
        reloaded.Domain.Should().Be("new.com");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Remove
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_ThenGetByOrgAndPlatform_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IDelegationCredentialRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var credential = DelegationCredential.Create(orgTenant, WorkspacePlatform.Google, "acme.com", "enc", SomeUserId);
        await repository.AddAsync(credential, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        repository.Remove(credential);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByOrgAndPlatformAsync(orgTenant.Id, WorkspacePlatform.Google, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<Tenant> SeedOrgTenantAsync(AccountDbContext dbContext)
    {
        var org = Tenant.CreateOrganization("owner@org.com", 0);
        dbContext.Set<Tenant>().Add(org);
        dbContext.Set<Subscription>().Add(Subscription.Create(org.Id));
        await dbContext.SaveChangesAsync();
        return org;
    }
}

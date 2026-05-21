using Account.Database;
using Account.Features.Smtp.Domain;
using Account.Features.Smtp.Infrastructure;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.Smtp;

/// <summary>
///     Database round-trip tests for <see cref="OrgSmtpConfigRepository" />.
///     <para>
///         All operations use the base <see cref="Provider" /> (no HTTP context → execution-context
///         TenantId is null).  <see cref="IOrgSmtpConfigRepository.GetByOrgIdAsync" /> explicitly calls
///         <c>IgnoreQueryFilters(Tenant)</c>, so no tenant-scoped provider is needed.
///     </para>
/// </summary>
public sealed class OrgSmtpConfigRepositoryTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // AddAsync + GetByIdAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ShouldRoundTripCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IOrgSmtpConfigRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var config = OrgSmtpConfig.Create(
            orgTenant,
            "smtp.acme.com",
            587,
            true,
            "noreply@acme.com",
            "enc_secret",
            "noreply@acme.com",
            "Acme Bookings",
            "support@acme.com"
        );

        // Act
        await repository.AddAsync(config, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(config.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(config.Id);
        loaded.TenantId.Should().Be(orgTenant.Id);
        loaded.Host.Should().Be("smtp.acme.com");
        loaded.Port.Should().Be(587);
        loaded.UseSsl.Should().BeTrue();
        loaded.Username.Should().Be("noreply@acme.com");
        loaded.EncryptedPassword.Should().Be("enc_secret");
        loaded.FromEmail.Should().Be("noreply@acme.com");
        loaded.FromName.Should().Be("Acme Bookings");
        loaded.ReplyToEmail.Should().Be("support@acme.com");
        loaded.IsEnabled.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetByOrgIdAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByOrgIdAsync_WhenConfigExists_ShouldReturnConfig()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IOrgSmtpConfigRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var config = OrgSmtpConfig.Create(orgTenant, "smtp.org.com", 465, true, "user", "enc", "from@org.com", null, null);
        await repository.AddAsync(config, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByOrgIdAsync(orgTenant.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(config.Id);
        result.Host.Should().Be("smtp.org.com");
    }

    [Fact]
    public async Task GetByOrgIdAsync_WhenNoConfigExists_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IOrgSmtpConfigRepository>();

        // Act
        var result = await repository.GetByOrgIdAsync(TenantId.NewId(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByOrgIdAsync_WithMultipleOrgs_ShouldReturnOnlyMatchingOrg()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IOrgSmtpConfigRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();

        var orgA = await SeedOrgTenantAsync(dbContext);
        var orgB = await SeedOrgTenantAsync(dbContext);

        var configA = OrgSmtpConfig.Create(orgA, "smtp.a.com", 587, false, "a", "eA", "a@a.com", null, null);
        var configB = OrgSmtpConfig.Create(orgB, "smtp.b.com", 465, true, "b", "eB", "b@b.com", null, null);
        await repository.AddAsync(configA, CancellationToken.None);
        await repository.AddAsync(configB, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        var resultA = await repository.GetByOrgIdAsync(orgA.Id, CancellationToken.None);
        var resultB = await repository.GetByOrgIdAsync(orgB.Id, CancellationToken.None);

        // Assert
        resultA!.Host.Should().Be("smtp.a.com");
        resultB!.Host.Should().Be("smtp.b.com");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update + Remove
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ThenGetByOrgIdAsync_ShouldReturnUpdatedValues()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IOrgSmtpConfigRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var config = OrgSmtpConfig.Create(orgTenant, "old.smtp.com", 25, false, "old_user", "old_enc", "old@from.com", null, null);
        await repository.AddAsync(config, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        config.Update("new.smtp.com", 465, true, "new_user", "new_enc", "new@from.com", "New Name", "reply@new.com");
        repository.Update(config);
        await dbContext.SaveChangesAsync();

        var reloaded = await repository.GetByOrgIdAsync(orgTenant.Id, CancellationToken.None);

        // Assert
        reloaded!.Host.Should().Be("new.smtp.com");
        reloaded.Port.Should().Be(465);
        reloaded.UseSsl.Should().BeTrue();
        reloaded.Username.Should().Be("new_user");
        reloaded.EncryptedPassword.Should().Be("new_enc");
        reloaded.FromEmail.Should().Be("new@from.com");
        reloaded.FromName.Should().Be("New Name");
        reloaded.ReplyToEmail.Should().Be("reply@new.com");
    }

    [Fact]
    public async Task Remove_ThenGetByOrgIdAsync_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var repository = sp.GetRequiredService<IOrgSmtpConfigRepository>();
        var dbContext = sp.GetRequiredService<AccountDbContext>();
        var orgTenant = await SeedOrgTenantAsync(dbContext);

        var config = OrgSmtpConfig.Create(orgTenant, "smtp.to.delete.com", 587, true, "u", "e", "f@d.com", null, null);
        await repository.AddAsync(config, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Act
        repository.Remove(config);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByOrgIdAsync(orgTenant.Id, CancellationToken.None);

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
        await dbContext.SaveChangesAsync();
        return org;
    }
}

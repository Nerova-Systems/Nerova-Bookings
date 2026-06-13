extern alias workers;

using Account.Database;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Tests.Persistence;
using workers::Account.Workers;
using Xunit;

namespace Account.Tests.Workers;

/// <summary>
///     Guards the local development seeder: it must seed the demo organization with teams, memberships,
///     org profile, and active tier flags on a fresh database, and be a strict no-op on re-run — a crash
///     here takes the whole account worker down at startup.
/// </summary>
public sealed class DevelopmentDataSeederTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task SeedAsync_OnFreshDatabase_ShouldCreateDemoOrganizationTeamsAndFlags()
    {
        // Act
        await RunSeederAsync();

        // Assert
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM tenants WHERE kind = 'Organization' AND name = 'Glow Beauty Group'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM tenants WHERE kind = 'Team' AND parent_tenant_id = (SELECT id FROM tenants WHERE name = 'Glow Beauty Group')", []).Should().Be(2);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM users WHERE email = 'owner@glow-demo.dev'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM memberships WHERE user_id = (SELECT id FROM users WHERE email = 'owner@glow-demo.dev')", []).Should().Be(3);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM org_profiles WHERE username = 'glow-owner'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM feature_flags WHERE flag_key = 'tier-teams' AND tenant_id IS NOT NULL AND enabled_at IS NOT NULL", []).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_ShouldBeIdempotent()
    {
        // Arrange
        await RunSeederAsync();

        // Act
        await RunSeederAsync();

        // Assert
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM users WHERE email = 'owner@glow-demo.dev'", []).Should().Be(1);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM tenants WHERE name = 'Glow Beauty Group'", []).Should().Be(1);
    }

    private async Task RunSeederAsync()
    {
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var seeder = new DevelopmentDataSeeder(dbContext, TimeProvider, NullLogger<DevelopmentDataSeeder>.Instance);
        await seeder.SeedAsync(CancellationToken.None);
    }
}

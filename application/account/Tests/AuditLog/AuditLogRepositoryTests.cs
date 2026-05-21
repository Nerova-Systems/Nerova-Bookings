using Account.Database;
using Account.Features.AuditLog.Domain;
using Account.Features.AuditLog.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Account.Tests.AuditLog;

/// <summary>
///     Database round-trip tests for <see cref="AuditLogRepository" />.
///     <para>
///         Write operations use the base <see cref="Provider" /> (null execution context — writes bypass
///         the <c>ITenantScopedEntity</c> query filter).
///     </para>
///     <para>
///         Paged query operations use a second <see cref="_tenantScopedProvider" /> built from the same
///         SQLite connection but with a mock <see cref="IExecutionContext" /> that returns
///         <see cref="DatabaseSeeder.Tenant1.Id" />, satisfying the global query filter.
///     </para>
/// </summary>
public sealed class AuditLogRepositoryTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // AddAsync + GetByIdAsync (bypasses tenant query filter via FindAsync)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ShouldRoundTripCorrectly()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var entry = AuditLogEntry.Create(
            DatabaseSeeder.Tenant1.Id,
            DatabaseSeeder.Tenant1Owner.Id,
            "owner@tenant-1.com",
            "Membership",
            "Invited",
            resourceId: "mbr_01TEST",
            metadata: """{"invitedEmail":"new@example.com"}""",
            ipAddress: "10.0.0.1",
            userAgent: "TestAgent/1.0"
        );

        // Act
        await repository.AddAsync(entry, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(entry.Id, CancellationToken.None);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(entry.Id);
        loaded.TenantId.Should().Be(DatabaseSeeder.Tenant1.Id);
        loaded.ActorUserId.Should().Be(DatabaseSeeder.Tenant1Owner.Id);
        loaded.ActorEmail.Should().Be("owner@tenant-1.com");
        loaded.Resource.Should().Be("Membership");
        loaded.Action.Should().Be("Invited");
        loaded.ResourceId.Should().Be("mbr_01TEST");
        loaded.Metadata.Should().Be("""{"invitedEmail":"new@example.com"}""");
        loaded.IpAddress.Should().Be("10.0.0.1");
        loaded.UserAgent.Should().Be("TestAgent/1.0");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        // Arrange
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        // Act
        var result = await repository.GetByIdAsync(AuditLogEntryId.NewId(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GetPagedAsync (requires tenant-scoped execution context)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_WhenEntriesExist_ShouldReturnMostRecentFirst()
    {
        // Arrange — seed via base provider (writes bypass tenant filter)
        await SeedEntryAsync("Booking", "Created");
        await SeedEntryAsync("Role", "Deleted");

        // Act — read via tenant-scoped provider
        using var tenantProvider = CreateTenantScopedProvider();
        using var scope = tenantProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var filter = new AuditLogFilter();
        var (items, totalCount) = await repository.GetPagedAsync(filter, 0, 25, CancellationToken.None);

        // Assert — at least 2 entries; most recent (Role/Deleted) should come first
        totalCount.Should().BeGreaterThanOrEqualTo(2);
        items.Should().HaveCountGreaterThanOrEqualTo(2);
        items[0].CreatedAt.Should().BeOnOrAfter(items[1].CreatedAt);
    }

    [Fact]
    public async Task GetPagedAsync_WhenFilteredByResource_ShouldReturnOnlyMatchingEntries()
    {
        // Arrange
        await SeedEntryAsync("Booking", "Created");
        await SeedEntryAsync("Role", "Deleted");

        // Act
        using var tenantProvider = CreateTenantScopedProvider();
        using var scope = tenantProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var filter = new AuditLogFilter(Resource: "Booking");
        var (items, totalCount) = await repository.GetPagedAsync(filter, 0, 25, CancellationToken.None);

        // Assert
        items.Should().OnlyContain(e => e.Resource == "Booking");
        totalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetPagedAsync_WhenFilteredByActorUserId_ShouldReturnOnlyThoseEntries()
    {
        // Arrange
        await SeedEntryAsync("Role", "Assigned", actorUserId: DatabaseSeeder.Tenant1Owner.Id);
        await SeedEntryAsync("Role", "Assigned", actorUserId: DatabaseSeeder.Tenant1Member.Id);

        // Act
        using var tenantProvider = CreateTenantScopedProvider();
        using var scope = tenantProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var filter = new AuditLogFilter(ActorUserId: DatabaseSeeder.Tenant1Owner.Id);
        var (items, totalCount) = await repository.GetPagedAsync(filter, 0, 25, CancellationToken.None);

        // Assert
        items.Should().OnlyContain(e => e.ActorUserId == DatabaseSeeder.Tenant1Owner.Id);
        totalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetPagedAsync_WithPagination_ShouldRespectPageSizeAndOffset()
    {
        // Arrange — seed 5 entries for the tenant
        for (var i = 0; i < 5; i++)
            await SeedEntryAsync("Membership", "Invited");

        // Act
        using var tenantProvider = CreateTenantScopedProvider();
        using var scope = tenantProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var filter = new AuditLogFilter(Resource: "Membership", Action: "Invited");
        var (page1, total) = await repository.GetPagedAsync(filter, 0, 2, CancellationToken.None);
        var (page2, _) = await repository.GetPagedAsync(filter, 1, 2, CancellationToken.None);

        // Assert
        total.Should().BeGreaterThanOrEqualTo(5);
        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page1.Select(e => e.Id).Should().NotIntersectWith(page2.Select(e => e.Id));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private ServiceProvider CreateTenantScopedProvider()
    {
        var tenantId = DatabaseSeeder.Tenant1.Id;
        var mockContext = Substitute.For<IExecutionContext>();
        mockContext.TenantId.Returns(tenantId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<AccountDbContext>(opts =>
            opts.UseSqlite(Connection).UseSnakeCaseNamingConvention());
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.RemoveAll<IExecutionContext>();
        services.AddScoped<IExecutionContext>(_ => mockContext);

        return services.BuildServiceProvider();
    }

    private async Task SeedEntryAsync(
        string resource,
        string action,
        UserId? actorUserId = null)
    {
        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var entry = AuditLogEntry.Create(
            DatabaseSeeder.Tenant1.Id,
            actorUserId ?? DatabaseSeeder.Tenant1Owner.Id,
            "owner@tenant-1.com",
            resource,
            action
        );

        await repository.AddAsync(entry, CancellationToken.None);
        await dbContext.SaveChangesAsync();
    }
}

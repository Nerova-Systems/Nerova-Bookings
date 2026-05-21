using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.AuditLog.Domain;
using Account.Features.AuditLog.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.AuditLog;

/// <summary>
///     End-to-end HTTP integration tests for the <c>GET /api/account/audit-log</c> endpoint.
///     Exercises the full MediatR pipeline: permission check → validation → handler → response mapping.
/// </summary>
public sealed class GetAuditLogTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string Url = "/api/account/audit-log";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLog_WhenMember_ShouldReturnForbidden()
    {
        var response = await AuthenticatedMemberHttpClient.GetAsync(Url);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_WhenOwner_ShouldReturnOk()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync(Url);

        response.ShouldBeSuccessfulGetRequest();
    }

    [Fact]
    public async Task GetAuditLog_WhenEntriesExist_ShouldReturnMappedResults()
    {
        // Arrange
        await SeedEntryAsync("Membership", "Invited");
        await SeedEntryAsync("Role", "Deleted");

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<AuditLogResponse>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        result.Entries.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Entries[0].CreatedAt.Should().BeOnOrAfter(result.Entries[1].CreatedAt);
    }

    [Fact]
    public async Task GetAuditLog_WhenEmpty_ShouldReturnEmptyPage()
    {
        // Act (no entries seeded for this test class instance)
        var response = await AuthenticatedOwnerHttpClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<AuditLogResponse>();
        result!.TotalCount.Should().Be(0);
        result.Entries.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetAuditLog_ResponseShape_ShouldIncludeAllExpectedFields()
    {
        // Arrange
        await SeedEntryAsync(
            "ApiKey",
            "Revoked",
            resourceId: "apikey_01TEST",
            metadata: """{"keyName":"CI/CD Key"}""",
            ipAddress: "192.168.0.1",
            userAgent: "TestAgent/1.0"
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(Url);
        var result = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        // Assert — verify response envelope
        result!.PageSize.Should().Be(25);
        result.CurrentPageOffset.Should().Be(0);
        result.TotalPages.Should().BeGreaterThanOrEqualTo(1);

        // Assert — verify entry fields
        var entry = result.Entries.First(e => e.Resource == "ApiKey" && e.Action == "Revoked");
        entry.Id.Should().NotBeNull();
        entry.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(10));
        entry.ActorEmail.Should().Be("owner@tenant-1.com");
        entry.ActorUserId.Should().Be(DatabaseSeeder.Tenant1Owner.Id.ToString());
        entry.ResourceId.Should().Be("apikey_01TEST");
        entry.Metadata.Should().Contain("keyName");
        entry.IpAddress.Should().Be("192.168.0.1");
        entry.UserAgent.Should().Be("TestAgent/1.0");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Filtering
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_WhenFilteredByResource_ShouldReturnOnlyMatchingEntries()
    {
        // Arrange
        await SeedEntryAsync("Workflow", "Created");
        await SeedEntryAsync("Schedule", "Deleted");

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"{Url}?Resource=Workflow");
        var result = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        // Assert
        result!.Entries.Should().OnlyContain(e => e.Resource == "Workflow");
    }

    [Fact]
    public async Task GetAuditLog_WhenFilteredByActorUserId_ShouldReturnOnlyThoseEntries()
    {
        // Arrange
        await SeedEntryAsync("Smtp", "Enabled", DatabaseSeeder.Tenant1Owner.Id);
        await SeedEntryAsync("Smtp", "Disabled", DatabaseSeeder.Tenant1Member.Id);

        // Act
        var ownerId = DatabaseSeeder.Tenant1Owner.Id.ToString();
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"{Url}?ActorUserId={ownerId}");
        var result = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        // Assert
        result!.Entries.Should().OnlyContain(e => e.ActorUserId == ownerId);
    }

    [Fact]
    public async Task GetAuditLog_WhenFilteredByInvalidActorUserId_ShouldReturnBadRequest()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"{Url}?ActorUserId=invalid-id-format");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pagination
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_WhenPageSizeIsZero_ShouldReturnBadRequest()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"{Url}?PageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAuditLog_WhenPageSizeExceeds100_ShouldReturnBadRequest()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"{Url}?PageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAuditLog_WhenPageOffsetIsNegative_ShouldReturnBadRequest()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"{Url}?PageOffset=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAuditLog_WithCustomPageSize_ShouldRespectPageSize()
    {
        // Arrange — seed 5 entries
        for (var i = 0; i < 5; i++)
        {
            await SeedEntryAsync("Sso", "Enabled");
        }

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"{Url}?Resource=Sso&Action=Enabled&PageSize=2");
        var result = await response.Content.ReadFromJsonAsync<AuditLogResponse>();

        // Assert
        result!.PageSize.Should().Be(2);
        result.Entries.Should().HaveCountLessOrEqualTo(2);
        result.TotalCount.Should().BeGreaterThanOrEqualTo(5);
        result.TotalPages.Should().BeGreaterThanOrEqualTo(3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task SeedEntryAsync(
        string resource,
        string action,
        UserId? actorUserId = null,
        string? resourceId = null,
        string? metadata = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        using var scope = Provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

        var entry = AuditLogEntry.Create(
            DatabaseSeeder.Tenant1.Id,
            actorUserId ?? DatabaseSeeder.Tenant1Owner.Id,
            "owner@tenant-1.com",
            resource,
            action,
            resourceId,
            metadata,
            ipAddress,
            userAgent
        );

        // Write directly to DbContext — bypasses ITenantScopedEntity query filter on writes.
        dbContext.Set<AuditLogEntry>().Add(entry);
        await dbContext.SaveChangesAsync();
    }
}

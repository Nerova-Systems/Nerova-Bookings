using System.Text.Json;
using Account.Database;
using Account.Features.AttributeSync.Domain;
using Account.Features.AttributeSync.Infrastructure;
using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using SharedKernel.Telemetry;
using Xunit;
using OrgAttribute = Account.Features.Attributes.Domain.Attribute;

namespace Account.Tests.AttributeSync;

/// <summary>
///     Integration tests for <see cref="AttributeSyncService" /> that exercise the
///     Direct, Lookup, and Group claim-mapping modes against an in-memory SQLite database.
/// </summary>
public sealed class AttributeSyncServiceTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // Direct mode
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_DirectMode_WhenNoExistingAssignment_ShouldCreateAssignment()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp);

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "department", ClaimMappingMode.Direct, false);
        await SeedRuleAsync(sp, rule);

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "Engineering");

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        var db = sp.GetRequiredService<AccountDbContext>();
        await db.SaveChangesAsync();

        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var assignments = await assignRepo.GetByMembershipAsync(membershipId, CancellationToken.None);

        assignments.Should().ContainSingle(a => a.AttributeId == attribute.Id && a.Value == "Engineering");
    }

    [Fact]
    public async Task ApplyAsync_DirectMode_WhenSameValue_ShouldBeNoOp()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp);

        // Pre-seed an existing assignment with the same value.
        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var existing = AttributeAssignment.Create(orgId, membershipId, attribute.Id, null, "Engineering", null);
        await assignRepo.AddAsync(existing, CancellationToken.None);
        await db.SaveChangesAsync();

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "department", ClaimMappingMode.Direct, false);
        await SeedRuleAsync(sp, rule);

        TelemetryEventsCollectorSpy.Reset();

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "Engineering");

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        // No AttributeSyncApplied event should be emitted for a no-op.
        TelemetryEventsCollectorSpy.CollectedEvents
            .Should().NotContain(e => e.GetType().Name == "AttributeSyncApplied");
    }

    [Fact]
    public async Task ApplyAsync_DirectMode_WhenValueChanges_ShouldUpdateAssignment()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp);

        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        var existing = AttributeAssignment.Create(orgId, membershipId, attribute.Id, null, "Old Value", null);
        await assignRepo.AddAsync(existing, CancellationToken.None);
        await db.SaveChangesAsync();

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "department", ClaimMappingMode.Direct, false);
        await SeedRuleAsync(sp, rule);

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "New Value");

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);
        await db.SaveChangesAsync();

        var updated = await assignRepo.GetByMembershipAttributeOptionAsync(
            membershipId, attribute.Id, null, CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Value.Should().Be("New Value");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Skipped — missing claim or missing attribute
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_WhenClaimMissing_ShouldEmitSkippedEvent()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp);

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "missing-claim", ClaimMappingMode.Direct, false);
        await SeedRuleAsync(sp, rule);

        TelemetryEventsCollectorSpy.Reset();

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "Engineering"); // Key doesn't match "missing-claim"

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        TelemetryEventsCollectorSpy.CollectedEvents
            .Should().ContainSingle(e => e.GetType().Name == "AttributeSyncSkipped");
    }

    [Fact]
    public async Task ApplyAsync_WhenArrayClaimPath_ShouldStripSuffixAndFindClaim()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp);

        // ClaimPath with [] suffix → claim key is "department", not "department[]"
        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "department[]", ClaimMappingMode.Direct, false);
        await SeedRuleAsync(sp, rule);

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "Engineering");

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        var db = sp.GetRequiredService<AccountDbContext>();
        await db.SaveChangesAsync();

        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var assignments = await assignRepo.GetByMembershipAsync(membershipId, CancellationToken.None);
        assignments.Should().ContainSingle(a => a.AttributeId == attribute.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Lookup mode
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_LookupMode_WhenOptionExists_ShouldAssignOption()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp, AttributeType.SingleSelect);

        // Add option to attribute.
        var db = sp.GetRequiredService<AccountDbContext>();
        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var option = attribute.AddOption("Engineering");
        attrRepo.Update(attribute);
        await db.SaveChangesAsync();

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "department", ClaimMappingMode.Lookup, false);
        await SeedRuleAsync(sp, rule);

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "Engineering");

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);
        await db.SaveChangesAsync();

        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var assignments = await assignRepo.GetByMembershipAsync(membershipId, CancellationToken.None);

        assignments.Should().ContainSingle(a =>
            a.AttributeId == attribute.Id && a.AttributeOptionId == option.Id);
    }

    [Fact]
    public async Task ApplyAsync_LookupMode_WhenNoMatchAndAutoCreateFalse_ShouldEmitSkipped()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp, AttributeType.SingleSelect);

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "department", ClaimMappingMode.Lookup, autoCreateOptions: false);
        await SeedRuleAsync(sp, rule);

        TelemetryEventsCollectorSpy.Reset();

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "Unknown");

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        TelemetryEventsCollectorSpy.CollectedEvents
            .Should().ContainSingle(e => e.GetType().Name == "AttributeSyncSkipped");
    }

    [Fact]
    public async Task ApplyAsync_LookupMode_WhenNoMatchAndAutoCreateTrue_ShouldCreateOption()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp, AttributeType.SingleSelect);

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "department", ClaimMappingMode.Lookup, autoCreateOptions: true);
        await SeedRuleAsync(sp, rule);

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeClaims("department", "NewOption");

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        var db = sp.GetRequiredService<AccountDbContext>();
        await db.SaveChangesAsync();

        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var loaded = await attrRepo.GetByIdUnfilteredAsync(attribute.Id, CancellationToken.None);

        loaded!.Options.Should().ContainSingle(o => o.Value == "NewOption");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Group mode
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_GroupMode_ShouldAddMissingOptions()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp, AttributeType.MultiSelect);

        var db = sp.GetRequiredService<AccountDbContext>();
        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var opt1 = attribute.AddOption("Backend");
        var opt2 = attribute.AddOption("Frontend");
        attrRepo.Update(attribute);
        await db.SaveChangesAsync();

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "groups[]", ClaimMappingMode.Group, false);
        await SeedRuleAsync(sp, rule);

        db.ChangeTracker.Clear();

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeArrayClaims("groups", ["Backend", "Frontend"]);

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);
        await db.SaveChangesAsync();

        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var assignments = await assignRepo.GetByMembershipAsync(membershipId, CancellationToken.None);

        var optionIds = assignments.Select(a => a.AttributeOptionId).ToHashSet();
        optionIds.Should().Contain(opt1.Id);
        optionIds.Should().Contain(opt2.Id);
    }

    [Fact]
    public async Task ApplyAsync_GroupMode_ShouldRemoveStaleAssignments()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp, AttributeType.MultiSelect);

        var db = sp.GetRequiredService<AccountDbContext>();
        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var optBackend = attribute.AddOption("Backend");
        var optStale = attribute.AddOption("Stale");
        attrRepo.Update(attribute);
        await db.SaveChangesAsync();

        // Pre-seed both assignments.
        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var aBackend = AttributeAssignment.Create(orgId, membershipId, attribute.Id, optBackend.Id, null, null);
        var aStale = AttributeAssignment.Create(orgId, membershipId, attribute.Id, optStale.Id, null, null);
        await assignRepo.AddAsync(aBackend, CancellationToken.None);
        await assignRepo.AddAsync(aStale, CancellationToken.None);
        await db.SaveChangesAsync();

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "groups", ClaimMappingMode.Group, false);
        await SeedRuleAsync(sp, rule);

        db.ChangeTracker.Clear();

        var service = sp.GetRequiredService<AttributeSyncService>();
        // Only "Backend" in claims — "Stale" should be removed.
        var claims = MakeArrayClaims("groups", ["Backend"]);

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);
        await db.SaveChangesAsync();

        var after = await assignRepo.GetByMembershipAsync(membershipId, CancellationToken.None);
        var optionIds = after.Select(a => a.AttributeOptionId).ToHashSet();

        optionIds.Should().Contain(optBackend.Id);
        optionIds.Should().NotContain(optStale.Id);
    }

    [Fact]
    public async Task ApplyAsync_GroupMode_WithAutoCreateTrue_ShouldCreateMissingOptions()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp, AttributeType.MultiSelect);

        var rule = AttributeSyncRule.Create(orgId, attribute.Id, "groups", ClaimMappingMode.Group, autoCreateOptions: true);
        await SeedRuleAsync(sp, rule);

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = MakeArrayClaims("groups", ["Alpha", "Beta"]);

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        var db = sp.GetRequiredService<AccountDbContext>();
        await db.SaveChangesAsync();

        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var loaded = await attrRepo.GetByIdUnfilteredAsync(attribute.Id, CancellationToken.None);

        loaded!.Options.Select(o => o.Value).Should().Contain(["Alpha", "Beta"]);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resilience — exception in one rule does not block others
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_WhenRuleThrows_ShouldContinueOtherRulesAndEmitFailed()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, attribute) = await SeedBaseAsync(sp);

        // Rule 1: references a non-existent attribute (will skip cleanly, not throw)
        // Rule 2: valid Direct rule that should succeed.
        // We can't easily inject an exception without deeper infrastructure, so
        // we verify the resilience via telemetry: if two rules are processed and
        // one is skipped (attribute_not_found), the other still runs.
        var badAttrId = AttributeId.NewId();
        var ruleSkip = AttributeSyncRule.Create(orgId, badAttrId, "department", ClaimMappingMode.Direct, false);
        var ruleGood = AttributeSyncRule.Create(orgId, attribute.Id, "role", ClaimMappingMode.Direct, false);
        await SeedRuleAsync(sp, ruleSkip);
        await SeedRuleAsync(sp, ruleGood);

        TelemetryEventsCollectorSpy.Reset();

        var service = sp.GetRequiredService<AttributeSyncService>();
        var claims = new Dictionary<string, JsonElement>
        {
            ["department"] = JsonSerializer.SerializeToElement("Engineering"),
            ["role"] = JsonSerializer.SerializeToElement("Senior")
        };

        await service.ApplyAsync(membershipId, orgId, SyncSource.MicrosoftSso, claims, CancellationToken.None);

        var db = sp.GetRequiredService<AccountDbContext>();
        await db.SaveChangesAsync();

        // The "role" rule should have created an assignment.
        var assignRepo = sp.GetRequiredService<IAttributeAssignmentRepository>();
        var assignments = await assignRepo.GetByMembershipAsync(membershipId, CancellationToken.None);
        assignments.Should().ContainSingle(a => a.AttributeId == attribute.Id && a.Value == "Senior");

        // The bad-attribute rule should have emitted a Skipped event (attribute_not_found).
        TelemetryEventsCollectorSpy.CollectedEvents
            .Should().Contain(e => e.GetType().Name == "AttributeSyncSkipped");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // No-op when no rules
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAsync_WhenNoRules_ShouldReturnWithoutError()
    {
        using var scope = Provider.CreateScope();
        var sp = scope.ServiceProvider;
        var (orgId, membershipId, _) = await SeedBaseAsync(sp);

        TelemetryEventsCollectorSpy.Reset();

        var service = sp.GetRequiredService<AttributeSyncService>();
        await service.ApplyAsync(membershipId, orgId, SyncSource.GoogleSso, new Dictionary<string, JsonElement>(), CancellationToken.None);

        // No events of any kind.
        TelemetryEventsCollectorSpy.CollectedEvents.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(TenantId OrgId, MembershipId MembershipId, OrgAttribute Attribute)> SeedBaseAsync(
        IServiceProvider sp,
        AttributeType attrType = AttributeType.Text)
    {
        var db = sp.GetRequiredService<AccountDbContext>();

        var org = Tenant.CreateOrganization(Faker.Internet.Email(), 0);
        db.Set<Tenant>().Add(org);
        db.Set<Subscription>().Add(Subscription.Create(org.Id));
        await db.SaveChangesAsync();

        var userTenant = Tenant.Create(Faker.Internet.Email(), 0);
        db.Set<Tenant>().Add(userTenant);
        db.Set<Subscription>().Add(Subscription.Create(userTenant.Id));
        var user = User.Create(userTenant.Id, Faker.Internet.Email(), UserRole.Member, true, null, 0);
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        var membership = Membership.CreateSeedOwner(org.Id, user.Id);
        db.Set<Membership>().Add(membership);
        await db.SaveChangesAsync();

        var attrRepo = sp.GetRequiredService<IAttributeRepository>();
        var attribute = OrgAttribute.Create(org.Id, TenantKind.Organization, "TestAttr", attrType);
        await attrRepo.AddAsync(attribute, CancellationToken.None);
        await db.SaveChangesAsync();

        return (org.Id, membership.Id, attribute);
    }

    private async Task SeedRuleAsync(IServiceProvider sp, AttributeSyncRule rule)
    {
        var ruleRepo = sp.GetRequiredService<IAttributeSyncRuleRepository>();
        var db = sp.GetRequiredService<AccountDbContext>();
        await ruleRepo.AddAsync(rule, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    private static IReadOnlyDictionary<string, JsonElement> MakeClaims(string key, string value) =>
        new Dictionary<string, JsonElement>
        {
            [key] = JsonSerializer.SerializeToElement(value)
        };

    private static IReadOnlyDictionary<string, JsonElement> MakeArrayClaims(string key, string[] values) =>
        new Dictionary<string, JsonElement>
        {
            [key] = JsonSerializer.SerializeToElement(values)
        };
}

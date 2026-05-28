using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Attributes;
using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.Attributes;

/// <summary>
///     End-to-end HTTP integration tests for the Attribute endpoints.
///     Exercises the full MediatR pipeline: auth → PBAC → feature-flag → handler → response.
/// </summary>
public sealed class AttributeEndpointTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string OrgAttributesUrl = "/api/account/org/attributes";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — anonymous
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttributes_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(OrgAttributesUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostAttribute_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAttribute_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.DeleteAsync($"{OrgAttributesUrl}/{AttributeId.NewId()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — requires ActiveOrgId
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttributes_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        // AuthenticatedOwnerHttpClient has no ActiveOrgId — solo context
        var response = await AuthenticatedOwnerHttpClient.GetAsync(OrgAttributesUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Feature flag guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttributes_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(OrgAttributesUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The attributes feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task PostAttribute_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The attributes feature is not enabled for this organization."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/org/attributes — create attribute
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostAttribute_HappyPath_ShouldReturnCreatedAttribute()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Department"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AttributeResponse>();
        body.Should().NotBeNull();
        body.Name.Should().Be("Department");
        body.Slug.Should().Be("department");
        body.Type.Should().Be(AttributeType.Text);
        body.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task PostAttribute_WithBlankName_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, new { Name = "", Type = "Text" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAttribute_WithDuplicateSlug_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Department"));
        var duplicate = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Department"));

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/org/attributes — list org attributes
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttributes_WhenNoneExist_ShouldReturnEmptyList()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(OrgAttributesUrl);

        response.ShouldBeSuccessfulGetRequest();
        var list = await response.Content.ReadFromJsonAsync<List<AttributeResponse>>();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAttributes_AfterCreating_ShouldReturnItInList()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Location"));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeResponse>();

        var listResp = await AnonymousHttpClient.GetAsync(OrgAttributesUrl);
        var list = await listResp.Content.ReadFromJsonAsync<List<AttributeResponse>>();

        list.Should().Contain(a => a.Id == created!.Id && a.Name == "Location");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/account/org/attributes/{id} — update attribute
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAttribute_HappyPath_ShouldUpdateName()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Old Name"));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeResponse>();

        var updateResp = await AnonymousHttpClient.PutAsJsonAsync(
            $"{OrgAttributesUrl}/{created!.Id}",
            new { Name = "New Name", IsLocked = false, IsWeightsEnabled = false, Enabled = true }
        );

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<AttributeResponse>();
        updated!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task PutAttribute_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"{OrgAttributesUrl}/{AttributeId.NewId()}",
            new { Name = "X", IsLocked = false, IsWeightsEnabled = false, Enabled = true }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /api/account/org/attributes/{id} — delete attribute
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAttribute_HappyPath_ShouldRemoveFromList()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("ToDelete"));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeResponse>();

        var deleteResp = await AnonymousHttpClient.DeleteAsync($"{OrgAttributesUrl}/{created!.Id}");
        deleteResp.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var listResp = await AnonymousHttpClient.GetAsync(OrgAttributesUrl);
        var list = await listResp.Content.ReadFromJsonAsync<List<AttributeResponse>>();
        list.Should().NotContain(a => a.Id == created.Id);
    }

    [Fact]
    public async Task DeleteAttribute_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{OrgAttributesUrl}/{AttributeId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/org/attributes/{id}/options — add option
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostOption_HappyPath_ShouldReturnNewOption()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl,
            new { Name = "Role", Type = "SingleSelect" }
        );
        var created = await createResp.Content.ReadFromJsonAsync<AttributeResponse>();

        var optResp = await AnonymousHttpClient.PostAsJsonAsync(
            $"{OrgAttributesUrl}/{created!.Id}/options",
            new { Value = "Engineer" }
        );

        optResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var option = await optResp.Content.ReadFromJsonAsync<AttributeOptionResponse>();
        option!.Value.Should().Be("Engineer");
    }

    [Fact]
    public async Task PostOption_OnTextAttribute_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Level"));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeResponse>();

        var optResp = await AnonymousHttpClient.PostAsJsonAsync(
            $"{OrgAttributesUrl}/{created!.Id}/options",
            new { Value = "Senior" }
        );

        optResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cross-org isolation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAttribute_ForDifferentOrg_ShouldReturnForbidden()
    {
        // Org1 creates an attribute.
        var org1Id = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org1Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org1Id);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Org1-Attr"));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeResponse>();

        // Org2 member tries to update it.
        var org2Id = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org2Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org2Id);

        var updateResp = await AnonymousHttpClient.PutAsJsonAsync(
            $"{OrgAttributesUrl}/{created!.Id}",
            new { Name = "Hijacked", IsLocked = false, IsWeightsEnabled = false, Enabled = true }
        );

        updateResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostAttribute_ForSoloTenant_ShouldReturnBadRequest()
    {
        // The authenticated owner's solo tenant is not an Organization.
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1.Id);

        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Solo-Attr"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Telemetry
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutOption_HappyPath_ShouldEmitAttributeOptionUpdatedEvent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl,
            new { Name = "Skill", Type = "SingleSelect" }
        );
        var created = await createResp.Content.ReadFromJsonAsync<AttributeResponse>();

        var optResp = await AnonymousHttpClient.PostAsJsonAsync(
            $"{OrgAttributesUrl}/{created!.Id}/options",
            new { Value = "Beginner" }
        );
        var option = await optResp.Content.ReadFromJsonAsync<AttributeOptionResponse>();

        TelemetryEventsCollectorSpy.Reset();

        var updateResp = await AnonymousHttpClient.PutAsJsonAsync(
            $"{OrgAttributesUrl}/{created.Id}/options/{option!.Id}",
            new { Value = "Expert", IsGroup = false, Contains = Array.Empty<string>() }
        );

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle();
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("AttributeOptionUpdated");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/account/org/members/{membershipId}/attributes/{attributeId} — assign
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutAssignment_HappyPath_ShouldReturnAssignment()
    {
        var orgId = InsertOrgTenant();
        var membershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var attrResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Level"));
        var attr = await attrResp.Content.ReadFromJsonAsync<AttributeResponse>();

        var assignResp = await AnonymousHttpClient.PutAsJsonAsync(
            $"/api/account/org/members/{membershipId}/attributes/{attr!.Id}",
            new { Value = "Senior", OptionId = (string?)null, Weight = (int?)null }
        );

        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var assignment = await assignResp.Content.ReadFromJsonAsync<AttributeAssignmentResponse>();
        assignment!.Value.Should().Be("Senior");
        assignment.AttributeId.Should().Be(attr.Id);
    }

    [Fact]
    public async Task DeleteAssignment_HappyPath_ShouldReturnSuccess()
    {
        var orgId = InsertOrgTenant();
        var membershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var attrResp = await AnonymousHttpClient.PostAsJsonAsync(OrgAttributesUrl, ValidCreatePayload("Region"));
        var attr = await attrResp.Content.ReadFromJsonAsync<AttributeResponse>();

        await AnonymousHttpClient.PutAsJsonAsync(
            $"/api/account/org/members/{membershipId}/attributes/{attr!.Id}",
            new { Value = "Europe", OptionId = (string?)null, Weight = (int?)null }
        );

        var deleteResp = await AnonymousHttpClient.DeleteAsync(
            $"/api/account/org/members/{membershipId}/attributes/{attr.Id}"
        );

        deleteResp.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private MembershipId InsertMembershipWithId(UserId userId, TenantId tenantId, MembershipRole role)
    {
        var id = MembershipId.NewId();
        var now = TimeProvider.GetUtcNow();
        Connection.Insert("memberships", [
                ("id", id.ToString()),
                ("tenant_id", tenantId.Value),
                ("user_id", userId.ToString()),
                ("role", role.ToString()),
                ("accepted", true),
                ("accepted_at", now),
                ("invited_by", null),
                ("invite_token", null),
                ("disable_impersonation", false),
                ("custom_role_id", null),
                ("created_at", now),
                ("modified_at", null)
            ]
        );
        return id;
    }

    private void SetActorToken(
        UserId actorId,
        TenantId actorTenantId,
        TenantId activeOrgId,
        string role = "Owner",
        HashSet<string>? featureFlags = null)
    {
        var userInfo = new UserInfo
        {
            IsAuthenticated = true,
            Id = actorId,
            TenantId = actorTenantId,
            Role = role,
            Email = "actor@test.com",
            Locale = "en-US",
            ActiveOrgId = activeOrgId,
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.CapAttributes.Key]
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void InsertMembership(UserId userId, TenantId tenantId, MembershipRole role)
    {
        var now = TimeProvider.GetUtcNow();
        Connection.Insert("memberships", [
                ("id", MembershipId.NewId().ToString()),
                ("tenant_id", tenantId.Value),
                ("user_id", userId.ToString()),
                ("role", role.ToString()),
                ("accepted", true),
                ("accepted_at", now),
                ("invited_by", null),
                ("invite_token", null),
                ("disable_impersonation", false),
                ("custom_role_id", null),
                ("created_at", now),
                ("modified_at", null)
            ]
        );
    }

    private TenantId InsertOrgTenant()
    {
        var orgId = TenantId.NewId();
        Connection.Insert("tenants", [
                ("id", orgId.Value),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("name", Faker.Company.CompanyName()),
                ("state", nameof(TenantState.Active)),
                ("logo", """{"Url":null,"Version":0}"""),
                ("plan", nameof(SubscriptionPlan.Basis)),
                ("rollout_bucket", 42),
                ("kind", nameof(TenantKind.Organization))
            ]
        );
        Connection.Insert("subscriptions", [
                ("tenant_id", orgId.Value),
                ("id", SubscriptionId.NewId().ToString()),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("plan", nameof(SubscriptionPlan.Basis)),
                ("scheduled_plan", null),
                ("paystack_customer_code", null),
                ("paystack_authorization_code", null),
                ("current_price_amount", null),
                ("current_price_currency", null),
                ("current_period_end", null),
                ("cancel_at_period_end", false),
                ("first_payment_failed_at", null),
                ("cancellation_reason", null),
                ("cancellation_feedback", null),
                ("payment_transactions", "[]"),
                ("payment_method", null),
                ("billing_info", null),
                ("has_drift_detected", false),
                ("drift_checked_at", null),
                ("drift_discrepancies", "[]")
            ]
        );
        return orgId;
    }

    private static object ValidCreatePayload(string name = "My Attribute")
    {
        return new { Name = name, Type = "Text" };
    }
}

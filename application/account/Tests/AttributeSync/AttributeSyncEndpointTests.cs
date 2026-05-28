using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Account.Database;
using Account.Features.Attributes.Domain;
using Account.Features.AttributeSync;
using Account.Features.AttributeSync.Domain;
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

namespace Account.Tests.AttributeSync;

/// <summary>
///     End-to-end HTTP integration tests for the AttributeSync endpoints.
///     Exercises the full MediatR pipeline: auth → PBAC → feature-flag → handler → response.
/// </summary>
public sealed class AttributeSyncEndpointTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string RulesUrl = "/api/account/org/attribute-sync/rules";
    private const string ApplyUrl = "/api/account/org/attribute-sync/apply";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — anonymous
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRules_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(RulesUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostRule_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("department"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutRule_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{RulesUrl}/{AttributeSyncRuleId.NewId()}", ValidUpdatePayload("department"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteRule_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.DeleteAsync($"{RulesUrl}/{AttributeSyncRuleId.NewId()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Apply_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(ApplyUrl, ValidApplyPayload(MembershipId.NewId()));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — no org context
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRules_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync(RulesUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Feature flag guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRules_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(RulesUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The IdP attribute sync feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task PostRule_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("department", attrId));

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The IdP attribute sync feature is not enabled for this organization."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/org/attribute-sync/rules
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRules_WhenNoneExist_ShouldReturnEmptyList()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(RulesUrl);

        response.ShouldBeSuccessfulGetRequest();
        var list = await response.Content.ReadFromJsonAsync<List<AttributeSyncRuleResponse>>();
        list.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetRules_AfterCreating_ShouldReturnInList()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("department", attrId));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();

        var listResp = await AnonymousHttpClient.GetAsync(RulesUrl);
        var list = await listResp.Content.ReadFromJsonAsync<List<AttributeSyncRuleResponse>>();

        list.Should().Contain(r => r.Id == created!.Id && r.ClaimPath == "department");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/org/attribute-sync/rules
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostRule_HappyPath_ShouldReturnCreatedRule()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("department", attrId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();
        body.Should().NotBeNull();
        body.ClaimPath.Should().Be("department");
        body.Mode.Should().Be(ClaimMappingMode.Direct);
        body.IsEnabled.Should().BeTrue();
        body.AutoCreateOptions.Should().BeFalse();
    }

    [Fact]
    public async Task PostRule_WithBlankClaimPath_ShouldReturnBadRequest()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl,
            new { AttributeId = attrId.ToString(), ClaimPath = "", Mode = "Direct" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostRule_WithUnknownAttributeId_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl,
            ValidCreatePayload("department", AttributeId.NewId())
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostRule_WithAttributeFromDifferentOrg_ShouldReturnForbidden()
    {
        // Org1 attribute
        var (org1Id, attr1Id) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org1Id, MembershipRole.Owner);

        // Org2 actor tries to use Org1's attribute
        var org2Id = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org2Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org2Id);

        var response = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl,
            ValidCreatePayload("department", attr1Id)
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostRule_ShouldEmitTelemetryEvent()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        TelemetryEventsCollectorSpy.Reset();

        await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("department", attrId));

        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e =>
            e.GetType().Name == "AttributeSyncRuleCreated"
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/account/org/attribute-sync/rules/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutRule_HappyPath_ShouldUpdateClaimPath()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("old-claim", attrId));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();

        var updateResp = await AnonymousHttpClient.PutAsJsonAsync(
            $"{RulesUrl}/{created!.Id}",
            ValidUpdatePayload("new-claim", attrId)
        );

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();
        updated!.ClaimPath.Should().Be("new-claim");
    }

    [Fact]
    public async Task PutRule_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"{RulesUrl}/{AttributeSyncRuleId.NewId()}",
            ValidUpdatePayload("claim")
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutRule_ForDifferentOrg_ShouldReturnForbidden()
    {
        // Org1 creates a rule.
        var (org1Id, attr1Id) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org1Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org1Id);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("claim", attr1Id));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();

        // Org2 actor tries to update it.
        var org2Id = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org2Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org2Id);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"{RulesUrl}/{created!.Id}",
            ValidUpdatePayload("hijacked")
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutRule_ShouldEmitTelemetryEvent()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("claim", attrId));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();

        TelemetryEventsCollectorSpy.Reset();

        await AnonymousHttpClient.PutAsJsonAsync($"{RulesUrl}/{created!.Id}", ValidUpdatePayload("updated-claim", attrId));

        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e =>
            e.GetType().Name == "AttributeSyncRuleUpdated"
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /api/account/org/attribute-sync/rules/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRule_HappyPath_ShouldRemoveFromList()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("claim", attrId));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();

        var deleteResp = await AnonymousHttpClient.DeleteAsync($"{RulesUrl}/{created!.Id}");
        deleteResp.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var listResp = await AnonymousHttpClient.GetAsync(RulesUrl);
        var list = await listResp.Content.ReadFromJsonAsync<List<AttributeSyncRuleResponse>>();
        list.Should().NotContain(r => r.Id == created.Id);
    }

    [Fact]
    public async Task DeleteRule_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{RulesUrl}/{AttributeSyncRuleId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteRule_ForDifferentOrg_ShouldReturnForbidden()
    {
        // Org1 creates a rule.
        var (org1Id, attr1Id) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org1Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org1Id);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("claim", attr1Id));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();

        // Org2 actor tries to delete it.
        var org2Id = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org2Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org2Id);

        var response = await AnonymousHttpClient.DeleteAsync($"{RulesUrl}/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRule_ShouldEmitTelemetryEvent()
    {
        var (orgId, attrId) = InsertOrgWithAttribute();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RulesUrl, ValidCreatePayload("claim", attrId));
        var created = await createResp.Content.ReadFromJsonAsync<AttributeSyncRuleResponse>();

        TelemetryEventsCollectorSpy.Reset();

        await AnonymousHttpClient.DeleteAsync($"{RulesUrl}/{created!.Id}");

        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e =>
            e.GetType().Name == "AttributeSyncRuleDeleted"
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/org/attribute-sync/apply
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_HappyPath_ShouldReturnSuccess()
    {
        var orgId = InsertOrgTenant();
        var membershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(ApplyUrl, ValidApplyPayload(membershipId));

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task Apply_WhenMembershipNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(ApplyUrl,
            ValidApplyPayload(MembershipId.NewId())
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Apply_WhenMembershipBelongsToDifferentOrg_ShouldReturnForbidden()
    {
        // Insert a membership in org1.
        var org1Id = InsertOrgTenant();
        var membershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Owner.Id, org1Id, MembershipRole.Owner);

        // Actor is in org2.
        var org2Id = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, org2Id, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, org2Id);

        var response = await AnonymousHttpClient.PostAsJsonAsync(ApplyUrl, ValidApplyPayload(membershipId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Apply_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        var membershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PostAsJsonAsync(ApplyUrl, ValidApplyPayload(membershipId));

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The IdP attribute sync feature is not enabled for this organization."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Creates an org tenant + a Text attribute for it, returning both IDs.
    ///     Used as a shortcut when a test needs a valid attribute to reference.
    /// </summary>
    private (TenantId OrgId, AttributeId AttrId) InsertOrgWithAttribute()
    {
        var orgId = InsertOrgTenant();
        var attrId = AttributeId.NewId();
        var now = TimeProvider.GetUtcNow();

        Connection.Insert("attributes", [
                ("id", attrId.ToString()),
                ("tenant_id", orgId.Value),
                ("name", Faker.Commerce.Department()),
                ("slug", Faker.Lorem.Word()),
                ("type", "Text"),
                ("is_weights_enabled", false),
                ("enabled", true),
                ("is_locked", false),
                ("created_at", now),
                ("modified_at", null)
            ]
        );

        return (orgId, attrId);
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
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.CapIntegrationAttributeSync.Key]
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

    private object ValidCreatePayload(string claimPath, AttributeId? attributeId = null)
    {
        return new
        {
            AttributeId = (attributeId ?? AttributeId.NewId()).ToString(),
            ClaimPath = claimPath,
            Mode = "Direct",
            AutoCreateOptions = false
        };
    }

    private object ValidUpdatePayload(string claimPath, AttributeId? attributeId = null)
    {
        return new
        {
            AttributeId = (attributeId ?? AttributeId.NewId()).ToString(),
            ClaimPath = claimPath,
            Mode = "Direct",
            AutoCreateOptions = false,
            IsEnabled = true
        };
    }

    private static object ValidApplyPayload(MembershipId membershipId)
    {
        return new
        {
            MembershipId = membershipId.ToString(),
            Claims = new Dictionary<string, JsonElement>
            {
                ["department"] = JsonSerializer.SerializeToElement("Engineering")
            }
        };
    }
}

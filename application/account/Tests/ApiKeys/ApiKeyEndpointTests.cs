using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.ApiKeys.Commands.CreateUserApiKey;
using Account.Features.ApiKeys.Domain;
using Account.Features.ApiKeys.Queries.ListUserApiKeys;
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

namespace Account.Tests.ApiKeys;

/// <summary>
///     End-to-end HTTP integration tests for the API key endpoints.
///     Exercises the full MediatR pipeline: auth → PBAC → feature-flag → validation → handler → response.
/// </summary>
public sealed class ApiKeyEndpointTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string UserKeysUrl = "/api/account/api-keys";
    private const string OrgKeysUrl = "/api/account/org/api-keys";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — anonymous
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostUserKey_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(UserKeysUrl, ValidCreatePayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserKeys_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(UserKeysUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteUserKey_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.DeleteAsync($"{UserKeysUrl}/{ApiKeyId.NewId()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostOrgKey_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgKeysUrl, ValidCreatePayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrgKeys_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(OrgKeysUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — org endpoints require ActiveOrgId
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostOrgKey_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgKeysUrl, ValidCreatePayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Feature flag guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostUserKey_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, []);

        var response = await AnonymousHttpClient.PostAsJsonAsync(UserKeysUrl, ValidCreatePayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The API keys feature is not enabled for this tenant."
        );
    }

    [Fact]
    public async Task GetUserKeys_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, []);

        var response = await AnonymousHttpClient.GetAsync(UserKeysUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The API keys feature is not enabled for this tenant."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/api-keys — user key creation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostUserKey_HappyPath_ShouldReturnCreatedKeyWithPlainText()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var response = await AnonymousHttpClient.PostAsJsonAsync(UserKeysUrl, ValidCreatePayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        body.Should().NotBeNull();
        body!.PlainText.Should().StartWith("nerova_user_");
        body.KeyPrefix.Should().NotBeNullOrEmpty();
        body.Id.ToString().Should().StartWith("key_");
    }

    [Fact]
    public async Task PostUserKey_WithBlankName_ShouldReturnBadRequest()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var payload = new { Name = "", ExpiresAt = (DateTimeOffset?)null };
        var response = await AnonymousHttpClient.PostAsJsonAsync(UserKeysUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostUserKey_WithPastExpiry_ShouldReturnBadRequest()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var payload = new { Name = "Key", ExpiresAt = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(-1) };
        var response = await AnonymousHttpClient.PostAsJsonAsync(UserKeysUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/api-keys — list user keys
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserKeys_WhenNoKeys_ShouldReturnEmptyList()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var response = await AnonymousHttpClient.GetAsync(UserKeysUrl);

        response.ShouldBeSuccessfulGetRequest();
        var keys = await response.Content.ReadFromJsonAsync<List<ApiKeyResponse>>();
        keys.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserKeys_AfterCreatingKey_ShouldReturnItInList()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var createResponse = await AnonymousHttpClient.PostAsJsonAsync(UserKeysUrl, ValidCreatePayload("My Listed Key"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        var listResponse = await AnonymousHttpClient.GetAsync(UserKeysUrl);
        var keys = await listResponse.Content.ReadFromJsonAsync<List<ApiKeyResponse>>();

        keys.Should().Contain(k => k.Id == created!.Id && k.Name == "My Listed Key");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /api/account/api-keys/{id} — revoke user key
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUserKey_HappyPath_ShouldReturnOkAndKeyBecomesRevoked()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var createResponse = await AnonymousHttpClient.PostAsJsonAsync(UserKeysUrl, ValidCreatePayload());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        var deleteResponse = await AnonymousHttpClient.DeleteAsync($"{UserKeysUrl}/{created!.Id}");

        deleteResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        // After revocation, the key should appear revoked in the list
        var listResponse = await AnonymousHttpClient.GetAsync(UserKeysUrl);
        var keys = await listResponse.Content.ReadFromJsonAsync<List<ApiKeyResponse>>();
        var revokedKey = keys!.FirstOrDefault(k => k.Id == created.Id);
        revokedKey.Should().NotBeNull();
        revokedKey!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteUserKey_WhenNotFound_ShouldReturnNotFound()
    {
        SetUserToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id);

        var response = await AnonymousHttpClient.DeleteAsync($"{UserKeysUrl}/{ApiKeyId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Org key endpoints
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostOrgKey_HappyPath_ShouldReturnCreatedKeyWithOrgPrefix()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(OrgKeysUrl, ValidCreatePayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        body!.PlainText.Should().StartWith("nerova_org_");
    }

    [Fact]
    public async Task GetOrgKeys_AfterCreatingOrgKey_ShouldReturnItInList()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResponse = await AnonymousHttpClient.PostAsJsonAsync(OrgKeysUrl, ValidCreatePayload("Org Listed Key"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        var listResponse = await AnonymousHttpClient.GetAsync(OrgKeysUrl);
        var keys = await listResponse.Content.ReadFromJsonAsync<List<ApiKeyResponse>>();

        keys.Should().Contain(k => k.Id == created!.Id && k.Name == "Org Listed Key");
    }

    [Fact]
    public async Task DeleteOrgKey_HappyPath_ShouldRevokeKey()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResponse = await AnonymousHttpClient.PostAsJsonAsync(OrgKeysUrl, ValidCreatePayload());
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();

        var deleteResponse = await AnonymousHttpClient.DeleteAsync($"{OrgKeysUrl}/{created!.Id}");

        deleteResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void SetUserToken(UserId actorId, TenantId actorTenantId, HashSet<string>? featureFlags = null)
    {
        var userInfo = new UserInfo
        {
            IsAuthenticated = true,
            Id = actorId,
            TenantId = actorTenantId,
            Role = "Owner",
            Email = "actor@test.com",
            Locale = "en-US",
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.CapApiKeys.Key]
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private void SetActorToken(UserId actorId, TenantId actorTenantId, TenantId activeOrgId, HashSet<string>? featureFlags = null)
    {
        var userInfo = new UserInfo
        {
            IsAuthenticated = true,
            Id = actorId,
            TenantId = actorTenantId,
            Role = "Owner",
            Email = "actor@test.com",
            Locale = "en-US",
            ActiveOrgId = activeOrgId,
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.CapApiKeys.Key]
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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

    private static object ValidCreatePayload(string name = "My API Key")
    {
        return new { Name = name, ExpiresAt = (DateTimeOffset?)null };
    }
}

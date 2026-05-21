using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.DelegationCredentials.Domain;
using Account.Features.DelegationCredentials.Queries.GetDelegationCredentials;
using Account.Features.Memberships.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.DelegationCredentials;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.DelegationCredentials;

/// <summary>
///     End-to-end HTTP integration tests for the delegation credential endpoints:
///     <c>GET /api/account/org/delegation-credentials</c>,
///     <c>PUT /api/account/org/delegation-credentials</c>,
///     <c>POST /api/account/org/delegation-credentials/{platform}/test</c>,
///     <c>PUT /api/account/org/delegation-credentials/{platform}/enable</c>,
///     <c>PUT /api/account/org/delegation-credentials/{platform}/disable</c>,
///     <c>DELETE /api/account/org/delegation-credentials/{platform}</c>.
///     Exercises the full MediatR pipeline: auth → PBAC → feature-flag → validation → handler → response.
/// </summary>
public sealed class DelegationCredentialEndpointTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string BaseUrl = "/api/account/org/delegation-credentials";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — anonymous
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCredentials_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutCredential_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTest_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/Google/test", ValidTestPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteCredential_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.DeleteAsync($"{BaseUrl}/Google");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — no org scope
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutCredential_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        // AuthenticatedOwnerHttpClient has no ActiveOrgId — solo context
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCredentials_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Feature flag guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutCredential_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The delegation credentials feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task GetCredentials_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(BaseUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The delegation credentials feature is not enabled for this organization."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — create (happy path)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutCredential_HappyPath_Create_ShouldReturnNoContent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task PutCredential_ThenGet_ShouldReturnPersistedCredential()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = ValidPutPayload();
        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, payload);

        var response = await AnonymousHttpClient.GetAsync(BaseUrl);
        response.ShouldBeSuccessfulGetRequest();

        var results = await response.DeserializeResponse<DelegationCredentialResponse[]>();
        results.Should().NotBeNull();
        results!.Should().ContainSingle();
        results[0].Platform.Should().Be(WorkspacePlatform.Google);
        results[0].Domain.Should().Be("acme.com");
        results[0].Status.Should().Be(DelegationCredentialStatus.Active);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — rotate (upsert on second call)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutCredential_CalledTwice_ShouldRotateExistingCredential()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        var updatedPayload = new
        {
            Platform = "Google",
            Domain = "updated.com",
            KeyBlob = "new_service_account_json"
        };
        var secondResponse = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, updatedPayload);
        secondResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await AnonymousHttpClient.GetAsync(BaseUrl);
        var results = await getResponse.DeserializeResponse<DelegationCredentialResponse[]>();
        results!.Should().ContainSingle();
        results[0].Domain.Should().Be("updated.com");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — validation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutCredential_WithAtSignInDomain_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new
        {
            Platform = "Google",
            Domain = "user@acme.com", // invalid — contains @
            KeyBlob = "some_blob"
        };

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutCredential_WithEmptyKeyBlob_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new
        {
            Platform = "Google",
            Domain = "acme.com",
            KeyBlob = "" // invalid — empty
        };

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCredential_HappyPath_ShouldReturnNoContent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        var response = await AnonymousHttpClient.DeleteAsync($"{BaseUrl}/Google");

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task DeleteCredential_WhenNoCredentialExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{BaseUrl}/Google");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Enable / Disable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisableCredential_ThenGetCredentials_ShouldReturnInactiveStatus()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());
        var disableResponse = await AnonymousHttpClient.PutAsync($"{BaseUrl}/Google/disable", null);
        disableResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await AnonymousHttpClient.GetAsync(BaseUrl);
        var results = await getResponse.DeserializeResponse<DelegationCredentialResponse[]>();
        results!.Should().ContainSingle();
        results[0].Status.Should().Be(DelegationCredentialStatus.Inactive);
    }

    [Fact]
    public async Task EnableCredential_WhenDisabled_ThenGetCredentials_ShouldReturnActiveStatus()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());
        await AnonymousHttpClient.PutAsync($"{BaseUrl}/Google/disable", null);

        var enableResponse = await AnonymousHttpClient.PutAsync($"{BaseUrl}/Google/enable", null);
        enableResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await AnonymousHttpClient.GetAsync(BaseUrl);
        var results = await getResponse.DeserializeResponse<DelegationCredentialResponse[]>();
        results![0].Status.Should().Be(DelegationCredentialStatus.Active);
    }

    [Fact]
    public async Task DisableCredential_WhenNoCredentialExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsync($"{BaseUrl}/Google/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /test
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostTest_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/Google/test", ValidTestPayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The delegation credentials feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task PostTest_WhenNoCredentialExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/Google/test", ValidTestPayload());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostTest_WithInvalidEmail_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new { MemberEmail = "not-an-email" };

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/Google/test", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostTest_WithValidCredential_ShouldReturnOkWithStubFailureResult()
    {
        // The NotConfiguredDelegationCredentialTester stub always returns Success=false.
        // This test confirms the full happy path flows through to a 200 response body.
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/Google/test", ValidTestPayload());

        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<TestDelegationCredentialResultResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse(); // stub always returns false until Wave 3
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET — returns empty array when no credentials
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCredentials_WhenNoCredentialsExist_ShouldReturnEmptyArray()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(BaseUrl);
        response.ShouldBeSuccessfulGetRequest();

        var results = await response.DeserializeResponse<DelegationCredentialResponse[]>();
        results.Should().NotBeNull();
        results!.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

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
                ("invited_by", (object?)null),
                ("invite_token", (object?)null),
                ("disable_impersonation", false),
                ("custom_role_id", (object?)null),
                ("created_at", now),
                ("modified_at", (object?)null)
            ]
        );
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
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.CapDelegationCredentials.Key]
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static object ValidPutPayload() => new
    {
        Platform = "Google",
        Domain = "acme.com",
        KeyBlob = """{"type":"service_account","project_id":"my-project"}"""
    };

    private static object ValidTestPayload() => new
    {
        MemberEmail = "member@acme.com"
    };

    /// <summary>Minimal deserialization shape for the test result response body.</summary>
    private sealed record TestDelegationCredentialResultResponse(bool Success, string? ErrorMessage);
}

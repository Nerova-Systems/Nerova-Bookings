using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.SsoMicrosoft.Queries;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.SsoMicrosoft;

/// <summary>
///     End-to-end HTTP integration tests for the Microsoft SSO configuration endpoints:
///     <c>GET/PUT/POST enable/POST disable/POST test /api/account/org/sso/microsoft</c>
///     and the SSO flow endpoints <c>GET /api/account/sso/microsoft/initiate|callback</c>.
///     Exercises the full MediatR pipeline: auth → PBAC → feature-flag → validation → handler → response.
/// </summary>
public sealed class MicrosoftSsoEndpointTests : EndpointBaseTest<AccountDbContext>, IClassFixture<AccountWebApplicationFactory>
{
    private const string MgmtBaseUrl = "/api/account/org/sso/microsoft";
    private const string FlowBaseUrl = "/api/account/sso/microsoft";

    private readonly AccountWebApplicationFactory _factory;

    // Explicit constructor (not primary) to avoid CS9107: 'factory' is used in the body via
    // _factory.CreateClient(...) for tests that need AllowAutoRedirect=false, and is also passed
    // to the base constructor. A primary constructor would double-capture it.
    // ReSharper disable once ConvertToPrimaryConstructor
    public MicrosoftSsoEndpointTests(AccountWebApplicationFactory factory) : base(factory)
    {
        _factory = factory;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — anonymous
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSso_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSso_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(MgmtBaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostEnableSso_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/enable", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostDisableSso_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/disable", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTestSso_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/test", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Feature flag guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSso_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The Microsoft SSO feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task GetSso_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(MgmtBaseUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The Microsoft SSO feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task PostTestSso_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/test", null);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The Microsoft SSO feature is not enabled for this organization."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — create (happy path)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSso_HappyPath_Create_ShouldReturnNoContent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task PutSso_ThenGet_ShouldReturnPersistedConfig()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = ValidPutPayload();
        await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, payload);

        var response = await AnonymousHttpClient.GetAsync(MgmtBaseUrl);
        response.ShouldBeSuccessfulGetRequest();

        var result = await response.Content.ReadFromJsonAsync<OrgMicrosoftSsoConfigResponse>();
        result.Should().NotBeNull();
        result.AzureTenantId.Should().Be("test-azure-tenant-id");
        result.ClientId.Should().Be("test-client-id");
        result.AllowedDomains.Should().Contain("acme.com");
        result.IsEnabled.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — update (idempotent upsert)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSso_CalledTwice_ShouldUpdateExistingConfig()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());

        var updatedPayload = new
        {
            AzureTenantId = "updated-azure-tenant",
            ClientId = "updated-client-id",
            ClientSecret = "updated_secret",
            AllowedDomains = new[] { "updated.com" }
        };
        await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, updatedPayload);

        var getResponse = await AnonymousHttpClient.GetAsync(MgmtBaseUrl);
        var result = await getResponse.Content.ReadFromJsonAsync<OrgMicrosoftSsoConfigResponse>();

        result!.AzureTenantId.Should().Be("updated-azure-tenant");
        result.ClientId.Should().Be("updated-client-id");
        result.AllowedDomains.Should().Contain("updated.com");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET — not found
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSso_WhenNoConfigExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(MgmtBaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /enable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostEnableSso_AfterDisabling_ShouldReEnableConfig()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());
        await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/disable", null);

        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/enable", null);

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
        var getResponse = await AnonymousHttpClient.GetAsync(MgmtBaseUrl);
        var config = await getResponse.Content.ReadFromJsonAsync<OrgMicrosoftSsoConfigResponse>();
        config!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task PostEnableSso_WhenAlreadyEnabled_ShouldBeIdempotent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());

        // Already enabled — calling enable again should be a no-op
        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/enable", null);

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task PostEnableSso_WhenNoConfigExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /disable
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostDisableSso_ShouldSetIsEnabledFalse()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());

        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/disable", null);

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
        var getResponse = await AnonymousHttpClient.GetAsync(MgmtBaseUrl);
        var config = await getResponse.Content.ReadFromJsonAsync<OrgMicrosoftSsoConfigResponse>();
        config!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task PostDisableSso_WhenAlreadyDisabled_ShouldBeIdempotent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, ValidPutPayload());
        await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/disable", null);

        // Already disabled — calling disable again should be a no-op
        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/disable", null);

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task PostDisableSso_WhenNoConfigExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /test
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostTestSso_WhenNoConfigExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostTestSso_WithBogusAzureTenantId_ShouldReturnOkWithFailureResult()
    {
        // The Azure tenant ID is invalid so discovery doc fetch fails;
        // the handler catches the exception and returns Success=false rather than 5xx.
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new
        {
            AzureTenantId = "bogus-tenant-xyz",
            ClientId = "some-client-id",
            ClientSecret = "some-secret",
            AllowedDomains = new[] { "acme.com" }
        };
        var putResponse = await AnonymousHttpClient.PutAsJsonAsync(MgmtBaseUrl, payload);
        putResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var response = await AnonymousHttpClient.PostAsync($"{MgmtBaseUrl}/test", null);

        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<TestMicrosoftSsoResultResponse>();
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /initiate — anonymous SSO flow entry point
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInitiate_WhenOrgNotFound_ShouldReturnNotFound()
    {
        var httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await httpClient.GetAsync($"{FlowBaseUrl}/initiate?org=non-existent-org-slug");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetInitiate_WhenSsoNotEnabled_ShouldReturnNotFound()
    {
        // The org exists but has no SSO config — should return 404
        var httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Use one of the seeded tenants; it has no SSO config by default
        var response = await httpClient.GetAsync($"{FlowBaseUrl}/initiate?org=tenant-1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /callback — anonymous SSO flow callback
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCallback_WhenErrorParamPresent_ShouldRedirectToErrorPage()
    {
        var httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await httpClient.GetAsync(
            $"{FlowBaseUrl}/callback?error=access_denied&error_description=User+denied+consent"
        );

        // The handler returns a failure redirect — the HTTP client should see a 302
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("error=");
    }

    [Fact]
    public async Task GetCallback_WhenStateMissing_ShouldRedirectToErrorPage()
    {
        var httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // No state, no cookie — should produce a failure redirect
        var response = await httpClient.GetAsync($"{FlowBaseUrl}/callback?code=some-code");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("error=");
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
                ("invited_by", null),
                ("invite_token", null),
                ("disable_impersonation", false),
                ("custom_role_id", null),
                ("created_at", now),
                ("modified_at", null)
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
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.CapSsoMicrosoft.Key]
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static object ValidPutPayload()
    {
        return new
        {
            AzureTenantId = "test-azure-tenant-id",
            ClientId = "test-client-id",
            ClientSecret = "test_secret",
            AllowedDomains = new[] { "acme.com", "acme.org" }
        };
    }

    /// <summary>Minimal deserialization shape for the TestMicrosoftSsoResult response body.</summary>
    private sealed record TestMicrosoftSsoResultResponse(bool Success, string? ErrorMessage);
}

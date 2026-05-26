using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Smtp.Queries;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.Smtp;

/// <summary>
///     End-to-end HTTP integration tests for the SMTP configuration endpoints:
///     <c>GET/PUT/DELETE /api/account/org/smtp</c> and <c>POST /api/account/org/smtp/test</c>.
///     Exercises the full MediatR pipeline: auth → PBAC → feature-flag → validation → handler → response.
/// </summary>
public sealed class ConfigureOrgSmtpTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string BaseUrl = "/api/account/org/smtp";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — no org scope
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSmtp_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSmtp_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteSmtp_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.DeleteAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTestSmtp_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/test", ValidTestPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — no org scope (user not in org context)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSmtp_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        // AuthenticatedOwnerHttpClient has no ActiveOrgId — solo context
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSmtp_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync(BaseUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Feature flag guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSmtp_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);

        // No cap-custom-smtp in feature flags
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The custom SMTP feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task GetSmtp_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(BaseUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The custom SMTP feature is not enabled for this organization."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — create (happy path)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSmtp_HappyPath_Create_ShouldReturnNoContent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task PutSmtp_ThenGet_ShouldReturnPersistedConfig()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = ValidPutPayload();
        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, payload);

        var response = await AnonymousHttpClient.GetAsync(BaseUrl);
        response.ShouldBeSuccessfulGetRequest();

        var result = await response.Content.ReadFromJsonAsync<OrgSmtpConfigResponse>();
        result.Should().NotBeNull();
        result.Host.Should().Be("smtp.example.com");
        result.Port.Should().Be(587);
        result.UseSsl.Should().BeTrue();
        result.Username.Should().Be("noreply@example.com");
        result.FromEmail.Should().Be("noreply@example.com");
        result.IsEnabled.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — update (idempotent upsert)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSmtp_CalledTwice_ShouldUpdateExistingConfig()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        var updatedPayload = new
        {
            Host = "updated.smtp.com",
            Port = 465,
            UseSsl = true,
            Username = "updated_user",
            Password = "updated_pass",
            FromEmail = "updated@from.com",
            FromName = "Updated Name",
            ReplyToEmail = (string?)null
        };
        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, updatedPayload);

        var getResponse = await AnonymousHttpClient.GetAsync(BaseUrl);
        var result = await getResponse.Content.ReadFromJsonAsync<OrgSmtpConfigResponse>();

        result!.Host.Should().Be("updated.smtp.com");
        result.Port.Should().Be(465);
        result.FromName.Should().Be("Updated Name");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT — validation
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSmtp_WithInvalidPort_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new
        {
            Host = "smtp.example.com",
            Port = 0, // invalid
            UseSsl = false,
            Username = "user",
            Password = "pass",
            FromEmail = "from@example.com"
        };

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutSmtp_WithInvalidFromEmail_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new
        {
            Host = "smtp.example.com",
            Port = 587,
            UseSsl = false,
            Username = "user",
            Password = "pass",
            FromEmail = "not-an-email"
        };

        var response = await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET — not found
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSmtp_WhenNoConfigExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSmtp_HappyPath_ShouldReturnNoContent()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());

        var response = await AnonymousHttpClient.DeleteAsync(BaseUrl);

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task DeleteSmtp_ThenGet_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PutAsJsonAsync(BaseUrl, ValidPutPayload());
        await AnonymousHttpClient.DeleteAsync(BaseUrl);

        var getResponse = await AnonymousHttpClient.GetAsync(BaseUrl);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSmtp_WhenNoConfigExists_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync(BaseUrl);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /test — validation and happy path
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostTestSmtp_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/test", ValidTestPayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The custom SMTP feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task PostTestSmtp_WithInvalidPayload_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new
        {
            Host = "smtp.example.com",
            Port = 99999, // invalid
            UseSsl = false,
            Username = "user",
            Password = "pass",
            FromEmail = "from@example.com",
            RecipientEmail = "recipient@example.com"
        };

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/test", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostTestSmtp_WithUnreachableServer_ShouldReturnOkWithFailureResult()
    {
        // The test SMTP server is unreachable; the handler catches the exception and returns
        // a TestOrgSmtpResult with Success = false rather than a 5xx status code.
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var payload = new
        {
            Host = "127.0.0.1", // localhost — no SMTP server listening
            Port = 19876, // unlikely to be bound
            UseSsl = false,
            Username = "testuser",
            Password = "testpass",
            FromEmail = "test@example.com",
            RecipientEmail = "recipient@example.com"
        };

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{BaseUrl}/test", payload);

        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<TestOrgSmtpResultResponse>();
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
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
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.CapCustomSmtp.Key]
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static object ValidPutPayload()
    {
        return new
        {
            Host = "smtp.example.com",
            Port = 587,
            UseSsl = true,
            Username = "noreply@example.com",
            Password = "super_secret",
            FromEmail = "noreply@example.com",
            FromName = "Example Bookings",
            ReplyToEmail = "support@example.com"
        };
    }

    private static object ValidTestPayload()
    {
        return new
        {
            Host = "smtp.example.com",
            Port = 587,
            UseSsl = true,
            Username = "noreply@example.com",
            Password = "super_secret",
            FromEmail = "noreply@example.com",
            RecipientEmail = "test@example.com"
        };
    }

    /// <summary>Minimal deserialization shape for the TestOrgSmtpResult response body.</summary>
    private sealed record TestOrgSmtpResultResponse(bool Success, string? ErrorMessage);
}

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.Impersonation;

/// <summary>
///     Integration tests for the impersonation start flow:
///     <c>POST /api/account/impersonation/start</c>.
/// </summary>
public sealed class StartImpersonationTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JwtSecurityToken DecodeToken(HttpResponseMessage response)
    {
        var token = response.Headers.GetValues("x-access-token").Single();
        return new JwtSecurityTokenHandler().ReadJwtToken(token);
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

    private UserId InsertUserInOrg(TenantId orgId, string email)
    {
        var userId = UserId.NewId();
        Connection.Insert("users", [
                ("tenant_id", orgId.Value),
                ("id", userId.ToString()),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("email", email),
                ("email_confirmed", true),
                ("first_name", Faker.Name.FirstName()),
                ("last_name", Faker.Name.LastName()),
                ("title", null),
                ("avatar", JsonSerializer.Serialize(new Avatar())),
                ("role", nameof(UserRole.Member)),
                ("locale", "en-US"),
                ("external_identities", "[]"),
                ("rollout_bucket", 42)
            ]
        );
        return userId;
    }

    private void InsertMembership(UserId userId, TenantId tenantId, MembershipRole role, bool disableImpersonation = false)
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
                ("disable_impersonation", disableImpersonation),
                ("custom_role_id", null),
                ("created_at", now),
                ("modified_at", null)
            ]
        );
    }

    private void SetActorToken(UserId actorId, TenantId actorTenantId, TenantId activeOrgId, string role = "Owner")
    {
        SetActorTokenInternal(actorId, actorTenantId, activeOrgId, role, new HashSet<string> { FeatureFlagDefinitions.CapImpersonation.Key });
    }

    private void SetActorTokenWithoutCapFlag(UserId actorId, TenantId actorTenantId, TenantId activeOrgId, string role = "Owner")
    {
        SetActorTokenInternal(actorId, actorTenantId, activeOrgId, role, new HashSet<string>());
    }

    private void SetActorTokenInternal(UserId actorId, TenantId actorTenantId, TenantId activeOrgId, string role, HashSet<string> featureFlags)
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
            FeatureFlags = featureFlags
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartImpersonation_HappyPath_ShouldReturnImpersonationToken()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var targetUserId = InsertUserInOrg(orgId, Faker.Internet.UniqueEmail());
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        InsertMembership(targetUserId, orgId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/account/impersonation/start",
            new { targetUserId = targetUserId.ToString() }
        );

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Subject.Should().Be(targetUserId.ToString());
        jwt.Claims.First(c => c.Type == "impersonated_by").Value
            .Should().Be(DatabaseSeeder.Tenant1Owner.Id.ToString());
    }

    [Fact]
    public async Task StartImpersonation_WhenCapFlagNotEnabled_ShouldReturnForbidden()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var targetUserId = InsertUserInOrg(orgId, Faker.Internet.UniqueEmail());
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        InsertMembership(targetUserId, orgId, MembershipRole.Member);

        // Actor has Owner permission but NO cap flag
        SetActorTokenWithoutCapFlag(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/account/impersonation/start",
            new { targetUserId = targetUserId.ToString() }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The impersonation feature is not enabled for this tenant."
        );
    }

    [Fact]
    public async Task StartImpersonation_WhenCallerIsAdmin_ShouldReturnForbidden()
    {
        // Arrange — Tenant1Member acts as Admin in the org; Admins lack User.Impersonate
        var orgId = InsertOrgTenant();
        var targetUserId = InsertUserInOrg(orgId, Faker.Internet.UniqueEmail());
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Admin);
        InsertMembership(targetUserId, orgId, MembershipRole.Member);

        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId, "Admin");

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/account/impersonation/start",
            new { targetUserId = targetUserId.ToString() }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StartImpersonation_WhenDisableImpersonationIsTrue_ShouldReturnForbidden()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var targetUserId = InsertUserInOrg(orgId, Faker.Internet.UniqueEmail());
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        InsertMembership(targetUserId, orgId, MembershipRole.Member, true);

        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/account/impersonation/start",
            new { targetUserId = targetUserId.ToString() }
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "This user has opted out of impersonation by organization admins."
        );
    }

    [Fact]
    public async Task StartImpersonation_WhenTargetNotInOrg_ShouldReturnNotFound()
    {
        // Arrange — outsiderUserId is never inserted in any tenant
        var orgId = InsertOrgTenant();
        var outsiderUserId = UserId.NewId();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/account/impersonation/start",
            new { targetUserId = outsiderUserId.ToString() }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartImpersonation_WhenNoActiveOrgScope_ShouldReturnForbidden()
    {
        // Arrange — JWT without ActiveOrgId; PermissionCheckBehavior rejects before handler runs
        var userInfo = new UserInfo
        {
            IsAuthenticated = true,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            TenantId = DatabaseSeeder.Tenant1.Id,
            Role = "Owner",
            Email = "actor@test.com",
            Locale = "en-US",
            // No ActiveOrgId
            FeatureFlags = new HashSet<string> { FeatureFlagDefinitions.CapImpersonation.Key }
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var targetUserId = UserId.NewId();

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/account/impersonation/start",
            new { targetUserId = targetUserId.ToString() }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

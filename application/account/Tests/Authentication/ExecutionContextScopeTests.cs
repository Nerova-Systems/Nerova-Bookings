using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Account.Database;
using Account.Features.Authentication.Commands;
using Account.Features.OrgProfiles.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentAssertions;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Authentication;

/// <summary>
///     Integration tests verifying that <see cref="SwitchTenantCommand" /> correctly derives and
///     encodes <c>ActiveTeamId</c>, <c>ActiveOrgId</c>, and <c>ActiveOrgProfileId</c> JWT claims
///     depending on the target tenant's <see cref="TenantKind" />.
/// </summary>
public sealed class ExecutionContextScopeTests(AccountWebApplicationFactory factory)
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
        InsertSubscription(orgId);
        return orgId;
    }

    private TenantId InsertTeamTenant(TenantId parentOrgId)
    {
        var teamId = TenantId.NewId();
        Connection.Insert("tenants", [
                ("id", teamId.Value),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("name", Faker.Company.CompanyName()),
                ("state", nameof(TenantState.Active)),
                ("logo", """{"Url":null,"Version":0}"""),
                ("plan", nameof(SubscriptionPlan.Basis)),
                ("rollout_bucket", 42),
                ("kind", nameof(TenantKind.Team)),
                ("parent_tenant_id", parentOrgId.Value)
            ]
        );
        InsertSubscription(teamId);
        return teamId;
    }

    private UserId InsertUserForTenant(TenantId tenantId, string email, bool emailConfirmed = true)
    {
        var userId = UserId.NewId();
        Connection.Insert("users", [
                ("tenant_id", tenantId.Value),
                ("id", userId.ToString()),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("email", email),
                ("email_confirmed", emailConfirmed),
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

    private OrgProfileId InsertOrgProfile(UserId userId, TenantId orgTenantId, string username)
    {
        var profileId = OrgProfileId.NewId();
        Connection.Insert("org_profiles", [
                ("id", profileId.Value),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("user_id", userId.ToString()),
                ("org_tenant_id", orgTenantId.Value),
                ("username", username),
                ("name", null),
                ("avatar_url", null),
                ("bio", null)
            ]
        );
        return profileId;
    }

    private void InsertSubscription(TenantId tenantId)
    {
        Connection.Insert("subscriptions", [
                ("tenant_id", tenantId.Value),
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
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SwitchTenant_ToSoloTenant_ShouldHaveNullScopeClaims()
    {
        // Arrange — a plain Solo tenant (the default kind when kind column is omitted)
        var soloId = TenantId.NewId();
        Connection.Insert("tenants", [
                ("id", soloId.Value),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("name", Faker.Company.CompanyName()),
                ("state", nameof(TenantState.Active)),
                ("logo", """{"Url":null,"Version":0}"""),
                ("plan", nameof(SubscriptionPlan.Basis)),
                ("rollout_bucket", 42)
            ]
        );
        InsertSubscription(soloId);
        InsertUserForTenant(soloId, DatabaseSeeder.Tenant1Member.Email);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            "/api/account/authentication/switch-tenant", new SwitchTenantCommand(soloId)
        );

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Claims.FirstOrDefault(c => c.Type == "active_team_id")?.Value.Should().BeNullOrEmpty();
        jwt.Claims.FirstOrDefault(c => c.Type == "active_org_id")?.Value.Should().BeNullOrEmpty();
        jwt.Claims.FirstOrDefault(c => c.Type == "active_org_profile_id")?.Value.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SwitchTenant_ToOrganizationTenant_ShouldSetActiveOrgIdAndNullTeamId()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var userId = InsertUserForTenant(orgId, DatabaseSeeder.Tenant1Member.Email);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            "/api/account/authentication/switch-tenant", new SwitchTenantCommand(orgId)
        );

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Claims.First(c => c.Type == "active_org_id").Value.Should().Be(orgId.ToString());
        jwt.Claims.FirstOrDefault(c => c.Type == "active_team_id")?.Value.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SwitchTenant_ToOrganizationTenant_WhenOrgProfileExists_ShouldSetActiveOrgProfileId()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var userId = InsertUserForTenant(orgId, DatabaseSeeder.Tenant1Member.Email);
        var profileId = InsertOrgProfile(userId, orgId, "member-username");

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            "/api/account/authentication/switch-tenant", new SwitchTenantCommand(orgId)
        );

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Claims.First(c => c.Type == "active_org_profile_id").Value.Should().Be(profileId.Value);
    }

    [Fact]
    public async Task SwitchTenant_ToTeamTenant_ShouldSetActiveTeamIdAndActiveOrgId()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var teamId = InsertTeamTenant(orgId);
        InsertUserForTenant(teamId, DatabaseSeeder.Tenant1Member.Email);
        // Also need user in the team's org so that GetParentOfAsync can find the org
        // (The handler switches to the *team* tenant's user, but OrgProfile lookup uses the org tenant)
        var userInOrg = InsertUserForTenant(orgId, Faker.Internet.UniqueEmail());

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            "/api/account/authentication/switch-tenant", new SwitchTenantCommand(teamId)
        );

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Claims.First(c => c.Type == "active_team_id").Value.Should().Be(teamId.ToString());
        jwt.Claims.First(c => c.Type == "active_org_id").Value.Should().Be(orgId.ToString());
    }

    [Fact]
    public async Task SwitchTenant_ToTeamTenant_WhenOrgProfileExistsInParentOrg_ShouldSetActiveOrgProfileId()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var teamId = InsertTeamTenant(orgId);
        var teamUserId = InsertUserForTenant(teamId, DatabaseSeeder.Tenant1Member.Email);
        var profileId = InsertOrgProfile(teamUserId, orgId, "team-member-slug");

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            "/api/account/authentication/switch-tenant", new SwitchTenantCommand(teamId)
        );

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Claims.First(c => c.Type == "active_org_profile_id").Value.Should().Be(profileId.Value);
    }

    [Fact]
    public async Task SwitchTenant_WithExplicitOrgProfileId_WhenProfileMatches_ShouldSucceed()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        var userId = InsertUserForTenant(orgId, DatabaseSeeder.Tenant1Member.Email);
        var profileId = InsertOrgProfile(userId, orgId, "explicit-user");

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            "/api/account/authentication/switch-tenant", new SwitchTenantCommand(orgId, profileId)
        );

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var jwt = DecodeToken(response);
        jwt.Claims.First(c => c.Type == "active_org_profile_id").Value.Should().Be(profileId.Value);
    }

    [Fact]
    public async Task SwitchTenant_WithExplicitOrgProfileId_WhenProfileDoesNotBelongToUser_ShouldReturnForbidden()
    {
        // Arrange — profile belongs to a different user
        var orgId = InsertOrgTenant();
        InsertUserForTenant(orgId, DatabaseSeeder.Tenant1Member.Email);

        // Create another user in the org and their profile
        var anotherUserId = InsertUserForTenant(orgId, Faker.Internet.UniqueEmail());
        var otherProfileId = InsertOrgProfile(anotherUserId, orgId, "another-user");

        // Act — attempt to switch with another user's profile
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(
            "/api/account/authentication/switch-tenant", new SwitchTenantCommand(orgId, otherProfileId)
        );

        // Assert
        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The specified OrgProfile does not belong to the user in this organization."
        );
    }
}

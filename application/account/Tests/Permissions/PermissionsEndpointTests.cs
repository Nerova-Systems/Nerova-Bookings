using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions;
using Account.Features.Permissions.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.Permissions;

/// <summary>
///     End-to-end HTTP integration tests for the PBAC (Role &amp; Permission) endpoints.
///     Exercises the full MediatR pipeline: auth → PBAC → tier-enterprise flag → handler → response.
/// </summary>
public sealed class PermissionsEndpointTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string RolesUrl = "/api/account/roles";
    private const string PermissionsUrl = "/api/account/permissions";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — anonymous
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoles_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(RolesUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostRole_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPermissions_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(PermissionsUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — requires ActiveOrgId (PermissionScope.Organization)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoles_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync(RolesUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Feature flag guard
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoles_WhenTierEnterpriseFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(RolesUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The custom roles feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task PostRole_WhenTierEnterpriseFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload());

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The custom roles feature is not enabled for this organization."
        );
    }

    [Fact]
    public async Task GetPermissions_WhenTierEnterpriseFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(PermissionsUrl);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.Forbidden,
            "The custom roles feature is not enabled for this organization."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/permissions — list permission catalog
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPermissions_HappyPath_ShouldReturnGroupedPermissions()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(PermissionsUrl);

        response.ShouldBeSuccessfulGetRequest();
        var groups = await response.Content.ReadFromJsonAsync<List<PermissionGroupResponse>>();
        groups.Should().NotBeNullOrEmpty();
        groups!.SelectMany(g => g.Permissions).Should().HaveCount(Permission.All.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/roles — create role
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostRole_HappyPath_ShouldReturnCreatedRole()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("Project Manager"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RoleResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Project Manager");
        body.IsSystem.Should().BeFalse();
        body.MemberCount.Should().Be(0);
        body.Permissions.Should().HaveCount(1);
    }

    [Fact]
    public async Task PostRole_WithBlankName_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            RolesUrl,
            new { Name = "", Description = (string?)null, Permissions = Array.Empty<string>() }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostRole_WithDuplicateName_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("Duplicate"));
        var duplicate = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("Duplicate"));

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostRole_WithInvalidPermissionKey_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(
            RolesUrl,
            new { Name = "Bad Perms", Description = (string?)null, Permissions = new[] { "not.a.real.permission" } }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/roles — list roles (system + custom)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoles_ShouldIncludeSystemAndCustomRoles()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("Custom Role"));
        var created = await createResp.Content.ReadFromJsonAsync<RoleResponse>();

        var listResp = await AnonymousHttpClient.GetAsync(RolesUrl);
        var list = await listResp.Content.ReadFromJsonAsync<List<RoleResponse>>();

        list.Should().NotBeNull();
        list!.Should().Contain(r => r.Id == created!.Id && r.Name == "Custom Role" && !r.IsSystem);
        list.Should().Contain(r => r.IsSystem); // system roles included
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/roles/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoleById_HappyPath_ShouldReturnRole()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("Lookup Me"));
        var created = await createResp.Content.ReadFromJsonAsync<RoleResponse>();

        var response = await AnonymousHttpClient.GetAsync($"{RolesUrl}/{created!.Id}");

        response.ShouldBeSuccessfulGetRequest();
        var body = await response.Content.ReadFromJsonAsync<RoleResponse>();
        body!.Name.Should().Be("Lookup Me");
    }

    [Fact]
    public async Task GetRoleById_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync($"{RolesUrl}/{RoleId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/account/roles/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PutRole_HappyPath_ShouldUpdateNameAndPermissions()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("Old Name"));
        var created = await createResp.Content.ReadFromJsonAsync<RoleResponse>();

        var updateResp = await AnonymousHttpClient.PutAsJsonAsync(
            $"{RolesUrl}/{created!.Id}",
            new { Name = "New Name", Description = "Updated", Permissions = new[] { "member.read", "member.update" } }
        );

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<RoleResponse>();
        updated!.Name.Should().Be("New Name");
        updated.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public async Task PutRole_WhenSystemRole_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"{RolesUrl}/{SystemRoles.OwnerId}",
            new { Name = "Tampered", Description = (string?)null, Permissions = Array.Empty<string>() }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutRole_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"{RolesUrl}/{RoleId.NewId()}",
            new { Name = "Nope", Description = (string?)null, Permissions = Array.Empty<string>() }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /api/account/roles/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRole_HappyPath_ShouldRemoveFromList()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("ToDelete"));
        var created = await createResp.Content.ReadFromJsonAsync<RoleResponse>();

        var deleteResp = await AnonymousHttpClient.DeleteAsync($"{RolesUrl}/{created!.Id}");
        deleteResp.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var listResp = await AnonymousHttpClient.GetAsync(RolesUrl);
        var list = await listResp.Content.ReadFromJsonAsync<List<RoleResponse>>();
        list!.Should().NotContain(r => r.Id == created.Id);
    }

    [Fact]
    public async Task DeleteRole_WhenSystemRole_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{RolesUrl}/{SystemRoles.AdminId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteRole_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{RolesUrl}/{RoleId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/account/memberships/{id}/role — assign role
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignRoleToMembership_HappyPath_ShouldSetCustomRole()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var memberId = InsertMembershipWithId(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var createResp = await AnonymousHttpClient.PostAsJsonAsync(RolesUrl, ValidCreatePayload("Assignable"));
        var created = await createResp.Content.ReadFromJsonAsync<RoleResponse>();

        var assignResp = await AnonymousHttpClient.PutAsJsonAsync(
            $"/api/account/memberships/{memberId}/role",
            new { RoleId = created!.Id.ToString() }
        );

        assignResp.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task AssignRoleToMembership_WithNullRoleId_ShouldClearCustomRole()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var memberId = InsertMembershipWithId(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"/api/account/memberships/{memberId}/role",
            new { RoleId = (string?)null }
        );

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    [Fact]
    public async Task AssignRoleToMembership_WithSystemRole_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var memberId = InsertMembershipWithId(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"/api/account/memberships/{memberId}/role",
            new { RoleId = SystemRoles.OwnerId.ToString() }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignRoleToMembership_WhenMembershipNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync(
            $"/api/account/memberships/{MembershipId.NewId()}/role",
            new { RoleId = (string?)null }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
                ("invited_by", (object?)null),
                ("invite_token", (object?)null),
                ("disable_impersonation", false),
                ("custom_role_id", (object?)null),
                ("created_at", now),
                ("modified_at", (object?)null)
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
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.TierEnterprise.Key]
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
                ("invited_by", (object?)null),
                ("invite_token", (object?)null),
                ("disable_impersonation", false),
                ("custom_role_id", (object?)null),
                ("created_at", now),
                ("modified_at", (object?)null)
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

    private static object ValidCreatePayload(string name = "Custom Role") =>
        new { Name = name, Description = (string?)null, Permissions = new[] { "member.read" } };
}

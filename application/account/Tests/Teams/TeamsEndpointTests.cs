using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Teams;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.Teams;

/// <summary>
///     End-to-end HTTP integration tests for the Teams endpoints (b4-teams-api).
///     Exercises the full MediatR pipeline: auth → tier-teams flag → handler → response.
/// </summary>
public sealed class TeamsEndpointTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string TeamsUrl = "/api/account/teams";
    private const string MembershipsUrl = "/api/account/memberships";

    // ──────────────────────────────────────────────────────────────────────────
    // Authorization — anonymous / no active org
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeams_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(TeamsUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostTeam_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(TeamsUrl, new { Name = "Acme" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTeams_WhenTierTeamsFlagNotEnabled_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId, featureFlags: []);

        var response = await AnonymousHttpClient.GetAsync(TeamsUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeams_WhenNoActiveOrg_ShouldReturnForbidden()
    {
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, activeOrgId: null);
        var response = await AnonymousHttpClient.GetAsync(TeamsUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/teams
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeams_HappyPath_ShouldReturnTeamsInActiveOrg()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Engineering");
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, teamId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(TeamsUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var teams = await response.Content.ReadFromJsonAsync<TeamResponse[]>();
        teams.Should().NotBeNull();
        teams!.Should().ContainSingle(t => t.Id == teamId);
        teams[0].Name.Should().Be("Engineering");
        teams[0].MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTeams_ShouldOnlyReturnTeamsOfActiveOrg()
    {
        var orgA = InsertOrgTenant();
        var orgB = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgA, MembershipRole.Owner);
        InsertTeamTenant(orgA, "TeamA");
        InsertTeamTenant(orgB, "TeamB");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgA);

        var response = await AnonymousHttpClient.GetAsync(TeamsUrl);

        var teams = await response.Content.ReadFromJsonAsync<TeamResponse[]>();
        teams!.Should().ContainSingle(t => t.Name == "TeamA");
        teams.Should().NotContain(t => t.Name == "TeamB");
    }

    [Fact]
    public async Task GetTeams_WhenCallerIsNotOrgMember_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync(TeamsUrl);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/teams
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTeam_HappyPath_ShouldCreateTeamAndSeedOwnerMembership()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(TeamsUrl, new { Name = "Engineering", Slug = "engineering" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        team.Should().NotBeNull();
        team!.Name.Should().Be("Engineering");
        team.Slug.Should().Be("engineering");
        team.ParentOrgId.Should().Be(orgId);
        team.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateTeam_WhenSlugAlreadyExistsInOrg_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        InsertTeamTenant(orgId, "Other", slug: "engineering");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(TeamsUrl, new { Name = "Engineering", Slug = "engineering" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTeam_WhenNameEmpty_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(TeamsUrl, new { Name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTeam_WhenCallerIsMember_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync(TeamsUrl, new { Name = "Engineering" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/teams/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamById_AsOrgOwner_ShouldReturnTeam()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Engineering");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync($"{TeamsUrl}/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        team!.Id.Should().Be(teamId);
    }

    [Fact]
    public async Task GetTeamById_WhenNotMemberAndNotOrgAdmin_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Engineering");
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync($"{TeamsUrl}/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamById_AsTeamMember_ShouldReturnTeam()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Engineering");
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, teamId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync($"{TeamsUrl}/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTeamById_WhenNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync($"{TeamsUrl}/{TenantId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTeamById_WhenTeamBelongsToOtherOrg_ShouldReturnForbidden()
    {
        var orgA = InsertOrgTenant();
        var orgB = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgA, MembershipRole.Owner);
        var foreignTeam = InsertTeamTenant(orgB, "OtherTeam");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgA);

        var response = await AnonymousHttpClient.GetAsync($"{TeamsUrl}/{foreignTeam}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUT /api/account/teams/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTeam_HappyPath_ShouldUpdateNameAndBranding()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Old");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId}",
            new { Name = "New", Slug = "new", Bio = "Hi", HideBranding = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var team = await response.Content.ReadFromJsonAsync<TeamResponse>();
        team!.Name.Should().Be("New");
        team.Slug.Should().Be("new");
        team.Bio.Should().Be("Hi");
        team.HideBranding.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTeam_WhenSlugConflictsWithSiblingTeam_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        InsertTeamTenant(orgId, "Sibling", slug: "taken");
        var teamId = InsertTeamTenant(orgId, "Mine", slug: "mine");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId}",
            new { Name = "Mine", Slug = "taken" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTeam_WhenCallerIsMember_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Eng");
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId}",
            new { Name = "New" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /api/account/teams/{id}
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteTeam_AsOrgOwner_ShouldSoftDeleteTeam()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Eng");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{TeamsUrl}/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTeam_AsOrgAdmin_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Admin);
        var teamId = InsertTeamTenant(orgId, "Eng");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{TeamsUrl}/{teamId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTeam_WhenTeamNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{TeamsUrl}/{TenantId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/account/teams/{id}/members
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamMembers_AsOrgOwner_ShouldReturnMembersWithUserInfo()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Eng");
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, teamId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync($"{TeamsUrl}/{teamId}/members");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(DatabaseSeeder.Tenant1Owner.Email);
        body.Should().Contain("\"role\":\"Owner\"");
    }

    [Fact]
    public async Task GetTeamMembers_WhenCallerHasNoAccess_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Eng");
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.GetAsync($"{TeamsUrl}/{teamId}/members");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/account/teams/{id}/invitations
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InviteTeamMember_WhenEmailUnknown_ShouldReturnBadRequestWithGapMessage()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Eng");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{TeamsUrl}/{teamId}/invitations",
            new { Email = "nobody@example.com", Role = nameof(MembershipRole.Member) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("signup-invite flow is not yet implemented");
    }

    [Fact]
    public async Task InviteTeamMember_WhenEmailExists_ShouldCreatePendingInvite()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Eng");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{TeamsUrl}/{teamId}/invitations",
            new { Email = DatabaseSeeder.Tenant1Member.Email, Role = nameof(MembershipRole.Member) });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InviteTeamMember_WhenUserAlreadyMember_ShouldReturnConflict()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Eng");
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, teamId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{TeamsUrl}/{teamId}/invitations",
            new { Email = DatabaseSeeder.Tenant1Member.Email, Role = nameof(MembershipRole.Member) });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InviteTeamMember_WhenCallerIsMember_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Eng");
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.PostAsJsonAsync($"{TeamsUrl}/{teamId}/invitations",
            new { Email = "any@test.com", Role = nameof(MembershipRole.Member) });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DELETE /api/account/memberships/{id} — RemoveMembership
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveMembership_AsOrgOwner_ShouldRemoveMember()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Eng");
        var memberId = InsertMembershipWithId(DatabaseSeeder.Tenant1Member.Id, teamId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{MembershipsUrl}/{memberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveMembership_WhenLastOwner_ShouldReturnBadRequest()
    {
        var orgId = InsertOrgTenant();
        var ownerMembershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{MembershipsUrl}/{ownerMembershipId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveMembership_WhenMemberRemovesSelf_ShouldSucceed()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Eng");
        var selfMembershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Member.Id, teamId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{MembershipsUrl}/{selfMembershipId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveMembership_WhenMemberRemovesOther_ShouldReturnForbidden()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Eng");
        var otherMembershipId = InsertMembershipWithId(DatabaseSeeder.Tenant1Owner.Id, teamId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{MembershipsUrl}/{otherMembershipId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveMembership_WhenMembershipNotFound_ShouldReturnNotFound()
    {
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        var response = await AnonymousHttpClient.DeleteAsync($"{MembershipsUrl}/{MembershipId.NewId()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveMembership_WhenMembershipBelongsToOtherOrg_ShouldReturnForbidden()
    {
        var orgA = InsertOrgTenant();
        var orgB = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgA, MembershipRole.Owner);
        var foreignTeam = InsertTeamTenant(orgB, "Other");
        var foreignMembership = InsertMembershipWithId(DatabaseSeeder.Tenant1Member.Id, foreignTeam, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgA);

        var response = await AnonymousHttpClient.DeleteAsync($"{MembershipsUrl}/{foreignMembership}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private TenantId InsertTeamTenant(TenantId parentOrgId, string name, string? slug = null)
    {
        var teamId = TenantId.NewId();
        Connection.Insert("tenants", [
                ("id", teamId.Value),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("name", name),
                ("state", nameof(TenantState.Active)),
                ("logo", """{"Url":null,"Version":0}"""),
                ("plan", nameof(SubscriptionPlan.Basis)),
                ("rollout_bucket", 7),
                ("kind", nameof(TenantKind.Team)),
                ("parent_tenant_id", parentOrgId.Value),
                ("slug", (object?)slug)
            ]
        );
        return teamId;
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
        TenantId? activeOrgId,
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
            FeatureFlags = featureFlags ?? [FeatureFlagDefinitions.TierTeams.Key]
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}

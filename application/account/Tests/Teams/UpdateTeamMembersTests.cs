using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Memberships.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests.Persistence;
using Xunit;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Tests.Teams;

/// <summary>
///     End-to-end HTTP integration tests for the bulk team-membership editor
///     (PUT /api/account/teams/{id}/members) — the one Teams command that previously had no coverage.
/// </summary>
public sealed class UpdateTeamMembersTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string TeamsUrl = "/api/account/teams";

    [Fact]
    public async Task UpdateTeamMembers_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{TenantId.NewId().Value}/members", new { addUserIds = Array.Empty<string>(), removeUserIds = Array.Empty<string>() });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateTeamMembers_WhenAddingExistingUser_ShouldCreateAcceptedMembership()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Styling");
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, teamId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId.Value}/members", new
            {
                addUserIds = new[] { DatabaseSeeder.Tenant1Member.Id.ToString() },
                removeUserIds = Array.Empty<string>()
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM memberships WHERE tenant_id = {teamId.Value} AND user_id = '{DatabaseSeeder.Tenant1Member.Id}' AND accepted = 1", []).Should().Be(1);
    }

    [Fact]
    public async Task UpdateTeamMembers_WhenAddingUserTwice_ShouldNotDuplicateMembership()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Styling");
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, teamId, MembershipRole.Owner);
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, teamId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId.Value}/members", new
            {
                addUserIds = new[] { DatabaseSeeder.Tenant1Member.Id.ToString() },
                removeUserIds = Array.Empty<string>()
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM memberships WHERE tenant_id = {teamId.Value} AND user_id = '{DatabaseSeeder.Tenant1Member.Id}'", []).Should().Be(1);
    }

    [Fact]
    public async Task UpdateTeamMembers_WhenRemovingMember_ShouldDeleteMembership()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Styling");
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, teamId, MembershipRole.Owner);
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, teamId, MembershipRole.Member);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId.Value}/members", new
            {
                addUserIds = Array.Empty<string>(),
                removeUserIds = new[] { DatabaseSeeder.Tenant1Member.Id.ToString() }
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM memberships WHERE tenant_id = {teamId.Value} AND user_id = '{DatabaseSeeder.Tenant1Member.Id}'", []).Should().Be(0);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM memberships WHERE tenant_id = {teamId.Value}", []).Should().Be(1, "the owner membership remains");
    }

    [Fact]
    public async Task UpdateTeamMembers_WhenRemovingLastOwner_ShouldReturnBadRequest()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgId, MembershipRole.Owner);
        var teamId = InsertTeamTenant(orgId, "Styling");
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, teamId, MembershipRole.Owner);
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgId);

        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId.Value}/members", new
            {
                addUserIds = Array.Empty<string>(),
                removeUserIds = new[] { DatabaseSeeder.Tenant1Owner.Id.ToString() }
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Connection.ExecuteScalar<long>($"SELECT COUNT(*) FROM memberships WHERE tenant_id = {teamId.Value}", []).Should().Be(1);
    }

    [Fact]
    public async Task UpdateTeamMembers_WhenCallerIsOrgMember_ShouldReturnForbidden()
    {
        // Arrange
        var orgId = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Member.Id, orgId, MembershipRole.Member);
        var teamId = InsertTeamTenant(orgId, "Styling");
        SetActorToken(DatabaseSeeder.Tenant1Member.Id, DatabaseSeeder.Tenant1.Id, orgId, "Member");

        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{teamId.Value}/members", new
            {
                addUserIds = new[] { DatabaseSeeder.Tenant1Owner.Id.ToString() },
                removeUserIds = Array.Empty<string>()
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTeamMembers_WhenTeamBelongsToOtherOrg_ShouldReturnForbidden()
    {
        // Arrange
        var orgA = InsertOrgTenant();
        var orgB = InsertOrgTenant();
        InsertMembership(DatabaseSeeder.Tenant1Owner.Id, orgA, MembershipRole.Owner);
        var foreignTeamId = InsertTeamTenant(orgB, "Foreign");
        SetActorToken(DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1.Id, orgA);

        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"{TeamsUrl}/{foreignTeamId.Value}/members", new
            {
                addUserIds = new[] { DatabaseSeeder.Tenant1Member.Id.ToString() },
                removeUserIds = Array.Empty<string>()
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        return orgId;
    }

    private TenantId InsertTeamTenant(TenantId parentOrgId, string name)
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
                ("parent_tenant_id", parentOrgId.Value)
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
                ("invited_by", null),
                ("invite_token", null),
                ("disable_impersonation", false),
                ("custom_role_id", null),
                ("created_at", now),
                ("modified_at", null)
            ]
        );
    }

    private void SetActorToken(UserId actorId, TenantId actorTenantId, TenantId? activeOrgId, string role = "Owner")
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
            FeatureFlags = new HashSet<string> { FeatureFlagDefinitions.TierTeams.Key }
        };
        var token = AccessTokenGenerator.Generate(userInfo);
        AnonymousHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}

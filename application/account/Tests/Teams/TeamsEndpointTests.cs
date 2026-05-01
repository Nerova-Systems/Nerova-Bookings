using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Account.Database;
using FluentAssertions;
using Xunit;

namespace Account.Tests.Teams;

public sealed class TeamsEndpointTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task TeamLifecycle_WhenFeatureEnabled_ShouldCreateUpdateListAndDeleteTeam()
    {
        await EnableTeamsAsync();

        var create = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/account/teams",
            new { name = "Cape Town Studio", description = "Primary service team" }
        );

        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonObject>();
        var teamId = created!["id"]!.GetValue<string>();
        created["name"]!.GetValue<string>().Should().Be("Cape Town Studio");

        var update = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/account/teams/{teamId}",
            new { name = "Sea Point Studio", description = "Front desk and providers" }
        );
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>("/api/account/teams");
        list!.Should().Contain(item => item!["id"]!.GetValue<string>() == teamId &&
                                      item["name"]!.GetValue<string>() == "Sea Point Studio");

        var delete = await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/account/teams/{teamId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        list = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>("/api/account/teams");
        list!.Should().NotContain(item => item!["id"]!.GetValue<string>() == teamId);
    }

    [Fact]
    public async Task TeamMembers_ShouldAddRemoveAndChangeRoles()
    {
        await EnableTeamsAsync();
        var teamId = await CreateTeamAsync();

        var updateMembers = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/account/teams/{teamId}/members",
            new
            {
                members = new[]
                {
                    new { userId = DatabaseSeeder.Tenant1Owner.Id.ToString(), role = "Admin" },
                    new { userId = DatabaseSeeder.Tenant1Member.Id.ToString(), role = "Member" }
                }
            }
        );

        updateMembers.StatusCode.Should().Be(HttpStatusCode.OK);
        var members = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>($"/api/account/teams/{teamId}/members");
        members!.Should().HaveCount(2);
        members.Should().Contain(member => member!["userId"]!.GetValue<string>() == DatabaseSeeder.Tenant1Member.Id.ToString() &&
                                           member["role"]!.GetValue<string>() == "Member");

        var changeRole = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/account/teams/{teamId}/members/{DatabaseSeeder.Tenant1Member.Id}/role",
            new { role = "Admin" }
        );

        changeRole.StatusCode.Should().Be(HttpStatusCode.OK);
        members = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>($"/api/account/teams/{teamId}/members");
        members!.Should().Contain(member => member!["userId"]!.GetValue<string>() == DatabaseSeeder.Tenant1Member.Id.ToString() &&
                                           member["role"]!.GetValue<string>() == "Admin");

        var removeMember = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/account/teams/{teamId}/members",
            new { members = new[] { new { userId = DatabaseSeeder.Tenant1Owner.Id.ToString(), role = "Admin" } } }
        );

        removeMember.StatusCode.Should().Be(HttpStatusCode.OK);
        members = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>($"/api/account/teams/{teamId}/members");
        members!.Should().ContainSingle(member => member!["userId"]!.GetValue<string>() == DatabaseSeeder.Tenant1Owner.Id.ToString());
    }

    private async Task EnableTeamsAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/account/feature-flags/teams/tenant-override",
            new { enabled = true }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> CreateTeamAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/account/teams",
            new { name = "Bookings Team", description = "Default team" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.Content.ReadFromJsonAsync<JsonObject>();
        return created!["id"]!.GetValue<string>();
    }
}

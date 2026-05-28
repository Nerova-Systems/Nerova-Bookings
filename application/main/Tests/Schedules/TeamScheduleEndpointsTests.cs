using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Schedules;

public sealed class TeamScheduleEndpointsTests : EndpointBaseTest<MainDbContext>
{
    private static readonly object DefaultScheduleCommand = new
    {
        name = "Team hours",
        timeZone = "UTC",
        isDefault = true,
        availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } }
    };

    [Fact]
    public async Task CreateTeamSchedule_WhenActiveTeamMatches_ShouldPersistTeamSchedule()
    {
        var teamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);

        var response = await teamClient.PostAsJsonAsync($"/api/teams/{teamId.Value}/schedules", DefaultScheduleCommand);
        response.EnsureSuccessStatusCode();
        var created = (await response.DeserializeResponse<ScheduleResponse>())!;

        created.Name.Should().Be("Team hours");
    }

    [Fact]
    public async Task CreateTeamSchedule_WhenActiveTeamMismatches_ShouldReturnForbidden()
    {
        var teamId = TenantId.NewId();
        var otherTeamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);

        var response = await teamClient.PostAsJsonAsync($"/api/teams/{otherTeamId.Value}/schedules", DefaultScheduleCommand);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Caller is not a member of the specified team.");
    }

    [Fact]
    public async Task CreateTeamSchedule_WhenNoActiveTeam_ShouldReturnForbidden()
    {
        var teamId = TenantId.NewId();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/teams/{teamId.Value}/schedules", DefaultScheduleCommand);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Caller is not a member of the specified team.");
    }

    [Fact]
    public async Task ListTeamSchedules_WhenActiveTeamMatches_ShouldReturnTeamSchedules()
    {
        var teamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);
        var createResponse = await teamClient.PostAsJsonAsync($"/api/teams/{teamId.Value}/schedules", DefaultScheduleCommand);
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.DeserializeResponse<ScheduleResponse>())!;

        var listResponse = await teamClient.GetAsync($"/api/teams/{teamId.Value}/schedules");
        listResponse.EnsureSuccessStatusCode();
        var list = (await listResponse.DeserializeResponse<SchedulesResponse>())!;

        list.Schedules.Select(schedule => schedule.Id).Should().Contain(created.Id);
    }

    [Fact]
    public async Task GetTeamSchedule_WhenActiveTeamMatches_ShouldReturnSchedule()
    {
        var teamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);
        var createResponse = await teamClient.PostAsJsonAsync($"/api/teams/{teamId.Value}/schedules", DefaultScheduleCommand);
        var created = (await createResponse.DeserializeResponse<ScheduleResponse>())!;

        var getResponse = await teamClient.GetAsync($"/api/teams/{teamId.Value}/schedules/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = (await getResponse.DeserializeResponse<ScheduleResponse>())!;

        fetched.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task UpdateTeamSchedule_WhenActiveTeamMatches_ShouldUpdateSchedule()
    {
        var teamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);
        var createResponse = await teamClient.PostAsJsonAsync($"/api/teams/{teamId.Value}/schedules", DefaultScheduleCommand);
        var created = (await createResponse.DeserializeResponse<ScheduleResponse>())!;

        var updateCommand = new
        {
            name = "Renamed",
            timeZone = "Europe/Copenhagen",
            isDefault = true,
            availabilityWindows = new[] { new { days = new[] { 2 }, startMinute = 600, endMinute = 720 } }
        };
        var updateResponse = await teamClient.PatchAsJsonAsync($"/api/teams/{teamId.Value}/schedules/{created.Id}", updateCommand);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.DeserializeResponse<ScheduleResponse>())!;

        updated.Name.Should().Be("Renamed");
        updated.TimeZone.Should().Be("Europe/Copenhagen");
    }

    [Fact]
    public async Task DeleteTeamSchedule_WhenActiveTeamMatches_ShouldDeleteSchedule()
    {
        var teamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);
        var firstResponse = await teamClient.PostAsJsonAsync($"/api/teams/{teamId.Value}/schedules", DefaultScheduleCommand);
        firstResponse.EnsureSuccessStatusCode();
        var secondCommand = new
        {
            name = "Secondary",
            timeZone = "UTC",
            isDefault = false,
            availabilityWindows = new[] { new { days = new[] { 1 }, startMinute = 600, endMinute = 720 } }
        };
        var secondResponse = await teamClient.PostAsJsonAsync($"/api/teams/{teamId.Value}/schedules", secondCommand);
        secondResponse.EnsureSuccessStatusCode();
        var secondary = (await secondResponse.DeserializeResponse<ScheduleResponse>())!;

        var deleteResponse = await teamClient.DeleteAsync($"/api/teams/{teamId.Value}/schedules/{secondary.Id}");
        deleteResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await teamClient.GetAsync($"/api/teams/{teamId.Value}/schedules/{secondary.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient CreateTeamHttpClient(TenantId teamId)
    {
        var owner = DatabaseSeeder.Tenant1Owner;
        var teamUserInfo = new UserInfo
        {
            Email = owner.Email,
            FirstName = owner.FirstName,
            LastName = owner.LastName,
            Id = owner.Id,
            IsAuthenticated = true,
            Locale = owner.Locale,
            Role = owner.Role,
            TenantId = owner.TenantId,
            ActiveTeamId = teamId
        };
        return CreateAuthenticatedHttpClient(teamUserInfo);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record SchedulesResponse(ScheduleResponse[] Schedules);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(
        string Id,
        string Name,
        string TimeZone,
        bool IsDefault,
        AvailabilityWindowResponse[] AvailabilityWindows
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record AvailabilityWindowResponse(int[] Days, int StartMinute, int EndMinute);
}

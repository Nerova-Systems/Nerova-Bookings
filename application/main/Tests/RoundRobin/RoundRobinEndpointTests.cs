using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features;
using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Shared;
using SharedKernel.Authentication;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.RoundRobin;

public sealed class RoundRobinEndpointTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _rrClient;

    public RoundRobinEndpointTests()
    {
        var ownerWithFlag = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Owner.Email,
            FirstName = DatabaseSeeder.Tenant1Owner.FirstName,
            LastName = DatabaseSeeder.Tenant1Owner.LastName,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Owner.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,
            TenantId = DatabaseSeeder.Tenant1Owner.TenantId,
            ActiveTeamId = DatabaseSeeder.TenantId,
            FeatureFlags = new HashSet<string> { RoundRobinAuthorization.RoundRobinFeatureFlagKey }
        };
        _rrClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    // ─── Feature flag gate ────────────────────────────────────────────────────

    [Fact]
    public async Task ListHosts_WhenFeatureFlagMissing_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/round-robin/{eventType.Id}/hosts");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, RoundRobinAuthorization.RoundRobinFeatureDisabledMessage);
    }

    [Fact]
    public async Task AddHost_WhenFeatureFlagMissing_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, RoundRobinAuthorization.RoundRobinFeatureDisabledMessage);
    }

    // ─── PBAC gate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AddHost_WhenMemberRole_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        var memberWithFlag = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Member.Email,
            FirstName = DatabaseSeeder.Tenant1Member.FirstName,
            LastName = DatabaseSeeder.Tenant1Member.LastName,
            Id = DatabaseSeeder.Tenant1Member.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Member.Locale,
            Role = "Member",
            TenantId = DatabaseSeeder.Tenant1Member.TenantId,
            FeatureFlags = new HashSet<string> { RoundRobinAuthorization.RoundRobinFeatureFlagKey }
        };
        var memberClient = CreateAuthenticatedHttpClient(memberWithFlag);

        var response = await memberClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, RoundRobinAuthorization.ManageRoundRobinHostsForbiddenMessage);
    }

    // ─── AddHost ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddHost_WhenOwnerAddsUser_ShouldReturnSuccessAndHostIsListed()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        response.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<RoundRobinHostAdded>().Should().ContainSingle();

        var hostsResponse = await _rrClient.GetAsync($"/api/round-robin/{eventType.Id}/hosts");
        hostsResponse.ShouldBeSuccessfulGetRequest();
        var hosts = await hostsResponse.DeserializeResponse<RoundRobinHostsApiResponse>();
        hosts!.Hosts.Should().ContainSingle(h => h.UserId == DatabaseSeeder.Tenant1Member.Id!.ToString());
    }

    [Fact]
    public async Task AddHost_WhenAlreadyAdded_ShouldReturnConflict()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        var second = await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        var memberId = DatabaseSeeder.Tenant1Member.Id!;
        await second.ShouldHaveErrorStatusCode(HttpStatusCode.Conflict, $"User '{memberId}' is already a host for this event type.");
    }

    [Fact]
    public async Task AddHost_WhenEventTypeNotFound_ShouldReturnNotFound()
    {
        var nonExistentId = EventTypeId.NewId();

        var response = await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{nonExistentId}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, (string?)null);
    }

    [Fact]
    public async Task AddHost_WhenEventTypeIsNotTeamScoped_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateSoloEventTypeAsync(schedule.Id);

        var response = await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Round-robin hosts can only be added to team-scoped event types.");
    }

    // ─── UpdateHost ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateHost_WhenHostExists_ShouldUpdateAndEmitTelemetry()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(memberId, false, 0, 100)
        );

        var updateResponse = await _rrClient.PutAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts/{memberId}",
            new UpdateRoundRobinHostRequest(true, 1, 200)
        );

        updateResponse.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<RoundRobinHostUpdated>().Should().ContainSingle();

        var hostsResponse = await _rrClient.GetAsync($"/api/round-robin/{eventType.Id}/hosts");
        var hosts = await hostsResponse.DeserializeResponse<RoundRobinHostsApiResponse>();
        var updatedHost = hosts!.Hosts.Single(h => h.UserId == memberId.ToString());
        updatedHost.IsFixed.Should().BeTrue();
        updatedHost.Priority.Should().Be(1);
        updatedHost.Weight.Should().Be(200);
    }

    [Fact]
    public async Task UpdateHost_WhenHostNotFound_ShouldReturnNotFound()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        var response = await _rrClient.PutAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts/{memberId}",
            new UpdateRoundRobinHostRequest(false, 0, 100)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"User '{memberId}' is not a host for this event type.");
    }

    // ─── RemoveHost ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveHost_WhenHostExists_ShouldReturnSuccessAndHostIsRemoved()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(memberId)
        );

        var response = await _rrClient.DeleteAsync($"/api/round-robin/{eventType.Id}/hosts/{memberId}");

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<RoundRobinHostRemoved>().Should().ContainSingle();

        var hostsResponse = await _rrClient.GetAsync($"/api/round-robin/{eventType.Id}/hosts");
        var hosts = await hostsResponse.DeserializeResponse<RoundRobinHostsApiResponse>();
        hosts!.Hosts.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveHost_WhenHostNotFound_ShouldReturnNotFound()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        var response = await _rrClient.DeleteAsync($"/api/round-robin/{eventType.Id}/hosts/{memberId}");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"User '{memberId}' is not a host for this event type.");
    }

    // ─── Delete cascade ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEventType_WhenHasHosts_ShouldCascadeDeleteHosts()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        await _rrClient.PostAsJsonAsync(
            $"/api/round-robin/{eventType.Id}/hosts",
            new AddRoundRobinHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/event-types/{eventType.Id}");

        var response = await _rrClient.GetAsync($"/api/round-robin/{eventType.Id}/hosts");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<EventTypeIdResponse> CreateTeamEventTypeAsync(string scheduleId)
    {
        var teamClient = CreateTeamEventTypeClient();
        return await CreateTeamEventTypeViaClientAsync(teamClient, scheduleId);
    }

    private async Task<EventTypeIdResponse> CreateSoloEventTypeAsync(string scheduleId)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", new
            {
                title = "Solo event",
                slug = $"solo-{Guid.NewGuid():N}",
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeIdResponse>())!;
    }

    private async Task<ScheduleIdResponse> CreateScheduleAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", new
            {
                name = "Work Hours",
                timeZone = "Africa/Johannesburg",
                isDefault = true,
                availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } }
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleIdResponse>())!;
    }

    private HttpClient CreateTeamEventTypeClient()
    {
        var ownerWithTeam = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Owner.Email,
            FirstName = DatabaseSeeder.Tenant1Owner.FirstName,
            LastName = DatabaseSeeder.Tenant1Owner.LastName,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Owner.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,
            TenantId = DatabaseSeeder.Tenant1Owner.TenantId,
            ActiveTeamId = DatabaseSeeder.TenantId
        };
        return CreateAuthenticatedHttpClient(ownerWithTeam);
    }

    private async Task<EventTypeIdResponse> CreateTeamEventTypeViaClientAsync(HttpClient client, string scheduleId)
    {
        var response = await client.PostAsJsonAsync("/api/event-types", new
            {
                title = "RR event",
                slug = $"rr-{Guid.NewGuid():N}",
                durationMinutes = 30,
                hidden = false,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeIdResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleIdResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeIdResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RoundRobinHostsApiResponse(RoundRobinHostApiResponse[] Hosts);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RoundRobinHostApiResponse(string EventTypeId, string UserId, bool IsFixed, int Priority, int Weight);
}

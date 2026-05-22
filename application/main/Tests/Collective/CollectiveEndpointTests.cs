using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features;
using Main.Features.Collective.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Collective;

public sealed class CollectiveEndpointTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _collectiveClient;

    public CollectiveEndpointTests()
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
            FeatureFlags = new HashSet<string> { CollectiveAuthorization.CollectiveFeatureFlagKey }
        };
        _collectiveClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    // ─── Feature flag gate ────────────────────────────────────────────────────

    [Fact]
    public async Task ListHosts_WhenFeatureFlagMissing_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/collective/{eventType.Id}/hosts");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, CollectiveAuthorization.CollectiveFeatureDisabledMessage);
    }

    [Fact]
    public async Task AddHost_WhenFeatureFlagMissing_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, CollectiveAuthorization.CollectiveFeatureDisabledMessage);
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
            FeatureFlags = new HashSet<string> { CollectiveAuthorization.CollectiveFeatureFlagKey }
        };
        var memberClient = CreateAuthenticatedHttpClient(memberWithFlag);

        var response = await memberClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, CollectiveAuthorization.ManageCollectiveHostsForbiddenMessage);
    }

    // ─── AddHost ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddHost_WhenOwnerAddsUser_ShouldReturnSuccessAndHostIsListed()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        response.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<CollectiveHostAdded>().Should().ContainSingle();

        var hostsResponse = await _collectiveClient.GetAsync($"/api/collective/{eventType.Id}/hosts");
        hostsResponse.ShouldBeSuccessfulGetRequest();
        var hosts = await hostsResponse.DeserializeResponse<CollectiveHostsApiResponse>();
        hosts!.Hosts.Should().ContainSingle(h => h.UserId == DatabaseSeeder.Tenant1Member.Id!.ToString());
    }

    [Fact]
    public async Task AddHost_WhenAlreadyAdded_ShouldReturnConflict()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);

        await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        var second = await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        var memberId = DatabaseSeeder.Tenant1Member.Id!;
        await second.ShouldHaveErrorStatusCode(HttpStatusCode.Conflict, $"User '{memberId}' is already a host for this event type.");
    }

    [Fact]
    public async Task AddHost_WhenEventTypeNotFound_ShouldReturnNotFound()
    {
        var nonExistentId = EventTypeId.NewId();

        var response = await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{nonExistentId}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, (string?)null);
    }

    [Fact]
    public async Task AddHost_WhenEventTypeIsNotTeamScoped_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateSoloEventTypeAsync(schedule.Id);

        var response = await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Collective hosts can only be added to team-scoped event types.");
    }

    // ─── RemoveHost ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveHost_WhenHostExists_ShouldReturnSuccessAndHostIsRemoved()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(memberId)
        );

        var response = await _collectiveClient.DeleteAsync($"/api/collective/{eventType.Id}/hosts/{memberId}");

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<CollectiveHostRemoved>().Should().ContainSingle();

        var hostsResponse = await _collectiveClient.GetAsync($"/api/collective/{eventType.Id}/hosts");
        var hosts = await hostsResponse.DeserializeResponse<CollectiveHostsApiResponse>();
        hosts!.Hosts.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveHost_WhenHostNotFound_ShouldReturnNotFound()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        var response = await _collectiveClient.DeleteAsync($"/api/collective/{eventType.Id}/hosts/{memberId}");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"User '{memberId}' is not a host for this event type.");
    }

    // ─── Delete cascade ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEventType_WhenHasHosts_ShouldCascadeDeleteHosts()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateTeamEventTypeAsync(schedule.Id);
        await _collectiveClient.PostAsJsonAsync(
            $"/api/collective/{eventType.Id}/hosts",
            new AddCollectiveHostRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        // Delete the event type
        await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/event-types/{eventType.Id}");

        // Hosts should be cascade-deleted
        var response = await _collectiveClient.GetAsync($"/api/collective/{eventType.Id}/hosts");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<EventTypeIdResponse> CreateTeamEventTypeAsync(string scheduleId)
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
        var teamClient = CreateAuthenticatedHttpClient(ownerWithTeam);

        var response = await teamClient.PostAsJsonAsync("/api/event-types", new
        {
            title = "Team event",
            slug = $"collective-{Guid.NewGuid():N}",
            durationMinutes = 30,
            hidden = false,
            scheduleId,
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 0
        });
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeIdResponse>())!;
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
        });
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
        });
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleIdResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleIdResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeIdResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CollectiveHostsApiResponse(CollectiveHostApiResponse[] Hosts);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CollectiveHostApiResponse(string EventTypeId, string UserId, bool IsFixed, int Priority, int Weight);
}

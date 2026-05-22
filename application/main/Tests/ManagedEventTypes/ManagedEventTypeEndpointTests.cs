using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using SharedKernel.Validation;
using Xunit;

namespace Main.Tests.ManagedEventTypes;

public sealed class ManagedEventTypeEndpointTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _managedClient;

    public ManagedEventTypeEndpointTests()
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
            FeatureFlags = new HashSet<string> { ManagedEventTypeAuthorization.ManagedEventTypesFeatureFlagKey }
        };
        _managedClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    // ─── Feature flag gate ───────────────────────────────────────────────────

    [Fact]
    public async Task ListChildren_WhenFeatureFlagMissing_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/managed-event-types/{parent.Id}/children");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "The managed event types feature is not enabled for your account.");
    }

    // ─── Assign ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Assign_WhenOwnerAssignsMember_ShouldCreateChildReplica()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        var response = await _managedClient.PostAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/assignments",
            new AssignManagedEventTypeRequest(memberId)
        );

        response.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<ManagedEventTypeAssigned>().Should().ContainSingle();

        var childrenResponse = await _managedClient.GetAsync($"/api/managed-event-types/{parent.Id}/children");
        childrenResponse.ShouldBeSuccessfulGetRequest();
        var children = await childrenResponse.DeserializeResponse<ManagedChildrenResponse>();
        children!.Children.Should().ContainSingle(c => c.MemberUserId == memberId.ToString());
    }

    [Fact]
    public async Task Assign_WhenAlreadyAssigned_ShouldReturnConflict()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        await _managedClient.PostAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/assignments",
            new AssignManagedEventTypeRequest(memberId)
        );

        var second = await _managedClient.PostAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/assignments",
            new AssignManagedEventTypeRequest(memberId)
        );

        await second.ShouldHaveErrorStatusCode(HttpStatusCode.Conflict, $"Member '{memberId}' is already assigned to this managed event type.");
    }

    [Fact]
    public async Task Assign_WhenMemberOnly_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);

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
            FeatureFlags = new HashSet<string> { ManagedEventTypeAuthorization.ManagedEventTypesFeatureFlagKey }
        };
        var memberClient = CreateAuthenticatedHttpClient(memberWithFlag);

        var response = await memberClient.PostAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/assignments",
            new AssignManagedEventTypeRequest(DatabaseSeeder.Tenant1Member.Id!)
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners and admins can manage managed event types.");
    }

    // ─── Unassign ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unassign_WhenMemberAssigned_ShouldRemoveChild()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        await _managedClient.PostAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/assignments",
            new AssignManagedEventTypeRequest(memberId)
        );

        var response = await _managedClient.DeleteAsync($"/api/managed-event-types/{parent.Id}/assignments/{memberId}");

        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var childrenResponse = await _managedClient.GetAsync($"/api/managed-event-types/{parent.Id}/children");
        var children = await childrenResponse.DeserializeResponse<ManagedChildrenResponse>();
        children!.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task Unassign_WhenNotAssigned_ShouldReturnNotFound()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        var response = await _managedClient.DeleteAsync($"/api/managed-event-types/{parent.Id}/assignments/{memberId}");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"Member '{memberId}' is not assigned to this managed event type.");
    }

    // ─── Sync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_WhenParentHasChildren_ShouldPropagateChanges()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;
        await _managedClient.PostAsJsonAsync($"/api/managed-event-types/{parent.Id}/assignments", new AssignManagedEventTypeRequest(memberId));

        // Update the parent via the standard event type endpoint
        await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/event-types/{parent.Id}", new
        {
            title = "Updated by owner",
            slug = "team-event",
            durationMinutes = 45,
            hidden = false,
            scheduleId = schedule.Id,
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 60
        });

        // Sync
        var syncResponse = await _managedClient.PostAsync($"/api/managed-event-types/{parent.Id}/sync", null);
        syncResponse.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<ManagedEventTypeSynced>().Should().ContainSingle();

        var childrenResponse = await _managedClient.GetAsync($"/api/managed-event-types/{parent.Id}/children");
        var children = await childrenResponse.DeserializeResponse<ManagedChildrenResponse>();
        children!.Children.Should().ContainSingle(c => c.Title == "Updated by owner");
    }

    // ─── UpdateLocks ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLocks_WhenOwnerUpdatesUnlockedFields_ShouldPersist()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await _managedClient.PutAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/locks",
            new UpdateManagedEventTypeLocksRequest(["title", "description"])
        );

        response.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<ManagedEventTypeLocksUpdated>().Should().ContainSingle();

        var statusResponse = await _managedClient.GetAsync($"/api/managed-event-types/{parent.Id}/status");
        var status = await statusResponse.DeserializeResponse<AssignmentStatusResponse>();
        status!.UnlockedFields.Should().BeEquivalentTo(["title", "description"]);
    }

    [Fact]
    public async Task UpdateLocks_WhenUnknownField_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);

        var response = await _managedClient.PutAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/locks",
            new UpdateManagedEventTypeLocksRequest(["unknownField"])
        );

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest,
            [new ErrorDetail("UnlockedFields", "Unknown fields: unknownField.")]);
    }

    // ─── Delete cascade ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEventType_WhenParentHasChildren_ShouldCascadeDeleteChildren()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        await _managedClient.PostAsJsonAsync($"/api/managed-event-types/{parent.Id}/assignments", new AssignManagedEventTypeRequest(memberId));

        var beforeResponse = await _managedClient.GetAsync($"/api/managed-event-types/{parent.Id}/children");
        var before = await beforeResponse.DeserializeResponse<ManagedChildrenResponse>();
        before!.Children.Should().ContainSingle();

        // Delete parent
        await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/event-types/{parent.Id}");

        // Children should be cascade-deleted — list returns 404
        var afterResponse = await _managedClient.GetAsync($"/api/managed-event-types/{parent.Id}/children");
        afterResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─── Lock enforcement ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEventType_WhenChildFieldIsLocked_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var parent = await CreateTeamEventTypeAsync(schedule.Id);
        var memberId = DatabaseSeeder.Tenant1Member.Id!;

        // Assign the member — creates a child replica owned by the member
        await _managedClient.PostAsJsonAsync(
            $"/api/managed-event-types/{parent.Id}/assignments",
            new AssignManagedEventTypeRequest(memberId)
        );

        // Get the child ID
        var childrenResponse = await _managedClient.GetAsync($"/api/managed-event-types/{parent.Id}/children");
        var children = await childrenResponse.DeserializeResponse<ManagedChildrenResponse>();
        var child = children!.Children.Single();

        // Use a client where the member has Owner role so CanManageSchedulingSetup passes,
        // while OwnerUserId still matches the child (child.OwnerUserId == memberId)
        var memberWithOwnerRole = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Member.Email,
            FirstName = DatabaseSeeder.Tenant1Member.FirstName,
            LastName = DatabaseSeeder.Tenant1Member.LastName,
            Id = memberId,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Member.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,  // Owner role to pass CanManageSchedulingSetup
            TenantId = DatabaseSeeder.Tenant1Member.TenantId,
            FeatureFlags = new HashSet<string> { ManagedEventTypeAuthorization.ManagedEventTypesFeatureFlagKey }
        };
        var memberOwnerClient = CreateAuthenticatedHttpClient(memberWithOwnerRole);

        // Attempt to change a locked field (title is locked by default — UnlockedFields is empty)
        var response = await memberOwnerClient.PutAsJsonAsync($"/api/event-types/{child.ChildId}", new
        {
            title = "Attempting to override locked title",
            slug = child.Slug,
            durationMinutes = 30,
            hidden = false,
            scheduleId = schedule.Id,  // lock check fires before schedule lookup, so any valid ScheduleId works
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 60
        });

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Fields title are locked by the managed template.");
        TelemetryEventsCollectorSpy.CollectedEvents.OfType<ManagedEventTypeFieldOverrideRejected>().Should().ContainSingle();
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
            slug = $"team-event-{Guid.NewGuid():N}",
            durationMinutes = 30,
            hidden = false,
            scheduleId,
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 60
        });
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeIdResponse>())!;
    }

    private async Task<ScheduleIdResponse> CreateScheduleAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", new
        {
            name = "Working hours",
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
    private sealed record ManagedChildrenResponse(ManagedChildResponse[] Children);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ManagedChildResponse(string ChildId, string MemberUserId, string Title, string Slug, string[] UnlockedFields);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record AssignmentStatusResponse(string ParentId, string[] UnlockedFields, ManagedChildResponse[] Children);
}

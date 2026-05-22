using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.TeamScoping;

/// <summary>
///     Tests that all scheduling aggregates correctly support optional team scoping.
/// </summary>
public sealed class TeamScopingDomainTests
{
    [Fact]
    public void Schedule_WhenCreatedWithoutTeamId_ShouldHaveNullTeamId()
    {
        var schedule = Schedule.Create(
            TenantId.NewId(), UserId.NewId(), "Working Hours", "UTC", true,
            [new AvailabilityWindow([1, 2, 3, 4, 5], 540, 1020)], []
        );

        schedule.TeamId.Should().BeNull();
    }

    [Fact]
    public void Schedule_WhenCreatedWithTeamId_ShouldHaveTeamId()
    {
        var teamId = TenantId.NewId();
        var schedule = Schedule.Create(
            TenantId.NewId(), UserId.NewId(), "Working Hours", "UTC", true,
            [new AvailabilityWindow([1, 2, 3, 4, 5], 540, 1020)], [],
            teamId
        );

        schedule.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void Schedule_AssignToTeam_ShouldSetTeamId()
    {
        var teamId = TenantId.NewId();
        var schedule = Schedule.Create(
            TenantId.NewId(), UserId.NewId(), "Working Hours", "UTC", true,
            [new AvailabilityWindow([1, 2, 3, 4, 5], 540, 1020)], []
        );

        schedule.AssignToTeam(teamId);

        schedule.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void Schedule_RemoveFromTeam_ShouldClearTeamId()
    {
        var teamId = TenantId.NewId();
        var schedule = Schedule.Create(
            TenantId.NewId(), UserId.NewId(), "Working Hours", "UTC", true,
            [new AvailabilityWindow([1, 2, 3, 4, 5], 540, 1020)], [],
            teamId
        );

        schedule.RemoveFromTeam();

        schedule.TeamId.Should().BeNull();
    }

    [Fact]
    public void EventType_WhenCreatedWithoutTeamId_ShouldHaveNullTeamId()
    {
        var eventType = EventType.Create(
            TenantId.NewId(), UserId.NewId(), "Intro Call", "intro-call", null, 30,
            false, ScheduleId.NewId(), 0, 0, 30, 60, null, null, null
        );

        eventType.TeamId.Should().BeNull();
    }

    [Fact]
    public void EventType_WhenCreatedWithTeamId_ShouldHaveTeamId()
    {
        var teamId = TenantId.NewId();
        var eventType = EventType.Create(
            TenantId.NewId(), UserId.NewId(), "Intro Call", "intro-call", null, 30,
            false, ScheduleId.NewId(), 0, 0, 30, 60, null, null, null, teamId
        );

        eventType.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void EventType_AssignToTeam_ThenRemoveFromTeam_ShouldRoundTrip()
    {
        var teamId = TenantId.NewId();
        var eventType = EventType.Create(
            TenantId.NewId(), UserId.NewId(), "Intro Call", "intro-call", null, 30,
            false, ScheduleId.NewId(), 0, 0, 30, 60, null, null, null
        );

        eventType.AssignToTeam(teamId);
        eventType.TeamId.Should().Be(teamId);

        eventType.RemoveFromTeam();
        eventType.TeamId.Should().BeNull();
    }

    [Fact]
    public void SchedulingProfile_WhenCreatedWithoutTeamId_ShouldHaveNullTeamId()
    {
        var profile = SchedulingProfile.Create(
            TenantId.NewId(), UserId.NewId(), "my-handle", "My Name", null
        );

        profile.TeamId.Should().BeNull();
    }

    [Fact]
    public void SchedulingProfile_WhenCreatedWithTeamId_ShouldHaveTeamId()
    {
        var teamId = TenantId.NewId();
        var profile = SchedulingProfile.Create(
            TenantId.NewId(), UserId.NewId(), "my-handle", "My Name", null, teamId
        );

        profile.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void SchedulingProfile_AssignToTeam_ShouldSetTeamId()
    {
        var teamId = TenantId.NewId();
        var profile = SchedulingProfile.Create(
            TenantId.NewId(), UserId.NewId(), "my-handle", "My Name", null
        );

        profile.AssignToTeam(teamId);

        profile.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void SchedulingProfile_RemoveFromTeam_ShouldClearTeamId()
    {
        var teamId = TenantId.NewId();
        var profile = SchedulingProfile.Create(
            TenantId.NewId(), UserId.NewId(), "my-handle", "My Name", null, teamId
        );

        profile.RemoveFromTeam();

        profile.TeamId.Should().BeNull();
    }

    [Fact]
    public void Booking_WhenCreatedWithoutTeamId_ShouldHaveNullTeamId()
    {
        var booking = Booking.Create(
            TenantId.NewId(), UserId.NewId(), EventTypeId.NewId(),
            DateTimeOffset.UtcNow, 30, 0, 0,
            "Alice", "alice@example.com", "UTC", BookingStatus.Accepted, new Dictionary<string, string>()
        );

        booking.TeamId.Should().BeNull();
    }

    [Fact]
    public void Booking_WhenCreatedWithTeamId_ShouldHaveTeamId()
    {
        var teamId = TenantId.NewId();
        var booking = Booking.Create(
            TenantId.NewId(), UserId.NewId(), EventTypeId.NewId(),
            DateTimeOffset.UtcNow, 30, 0, 0,
            "Alice", "alice@example.com", "UTC", BookingStatus.Accepted, new Dictionary<string, string>(),
            teamId
        );

        booking.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void Booking_AssignToTeam_ShouldSetTeamId()
    {
        var teamId = TenantId.NewId();
        var booking = Booking.Create(
            TenantId.NewId(), UserId.NewId(), EventTypeId.NewId(),
            DateTimeOffset.UtcNow, 30, 0, 0,
            "Alice", "alice@example.com", "UTC", BookingStatus.Accepted, new Dictionary<string, string>()
        );

        booking.AssignToTeam(teamId);

        booking.TeamId.Should().Be(teamId);
    }
}

/// <summary>
///     Integration tests that verify team-scoped queries isolate data correctly from solo-user data.
/// </summary>
public sealed class TeamScopingIntegrationTests : EndpointBaseTest<MainDbContext>
{
    private static readonly object DefaultScheduleCommand = new
    {
        name = "Working Hours",
        timeZone = "UTC",
        isDefault = true,
        availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } }
    };

    [Fact]
    public async Task GetSchedules_WhenTeamSessionActive_ShouldOnlyReturnTeamSchedules()
    {
        // Arrange: create a solo schedule (no ActiveTeamId)
        var soloResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", DefaultScheduleCommand);
        soloResponse.EnsureSuccessStatusCode();
        var soloSchedule = (await soloResponse.DeserializeResponse<ScheduleResponse>())!;

        // Arrange: create a team schedule (with ActiveTeamId)
        var teamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);
        var teamResponse = await teamClient.PostAsJsonAsync("/api/schedules", DefaultScheduleCommand);
        teamResponse.EnsureSuccessStatusCode();
        var teamSchedule = (await teamResponse.DeserializeResponse<ScheduleResponse>())!;

        // Act: get schedules as team user
        var getTeamResponse = await teamClient.GetAsync("/api/schedules");
        getTeamResponse.EnsureSuccessStatusCode();
        var teamSchedules = (await getTeamResponse.DeserializeResponse<SchedulesResponse>())!;

        // Act: get schedules as solo user
        var getSoloResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/schedules");
        getSoloResponse.EnsureSuccessStatusCode();
        var soloSchedules = (await getSoloResponse.DeserializeResponse<SchedulesResponse>())!;

        // Assert: team session only sees team schedules
        teamSchedules.Schedules.Select(s => s.Id).Should().Contain(teamSchedule.Id);
        teamSchedules.Schedules.Select(s => s.Id).Should().NotContain(soloSchedule.Id);

        // Assert: solo session only sees solo schedules
        soloSchedules.Schedules.Select(s => s.Id).Should().Contain(soloSchedule.Id);
        soloSchedules.Schedules.Select(s => s.Id).Should().NotContain(teamSchedule.Id);
    }

    [Fact]
    public async Task GetEventTypes_WhenTeamSessionActive_ShouldOnlyReturnTeamEventTypes()
    {
        // Arrange: solo schedule and event type
        var soloScheduleResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", DefaultScheduleCommand);
        soloScheduleResponse.EnsureSuccessStatusCode();
        var soloSchedule = (await soloScheduleResponse.DeserializeResponse<ScheduleResponse>())!;

        var soloEventTypeResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/event-types", NewEventTypeRequest(soloSchedule.Id, "Solo Event", "solo-event")
        );
        soloEventTypeResponse.EnsureSuccessStatusCode();
        var soloEventType = (await soloEventTypeResponse.DeserializeResponse<EventTypeResponse>())!;

        // Arrange: team session schedule and event type
        var teamId = TenantId.NewId();
        var teamClient = CreateTeamHttpClient(teamId);

        var teamScheduleResponse = await teamClient.PostAsJsonAsync("/api/schedules", DefaultScheduleCommand);
        teamScheduleResponse.EnsureSuccessStatusCode();
        var teamSchedule = (await teamScheduleResponse.DeserializeResponse<ScheduleResponse>())!;

        var teamEventTypeResponse = await teamClient.PostAsJsonAsync(
            "/api/event-types", NewEventTypeRequest(teamSchedule.Id, "Team Event", "team-event")
        );
        teamEventTypeResponse.EnsureSuccessStatusCode();
        var teamEventType = (await teamEventTypeResponse.DeserializeResponse<EventTypeResponse>())!;

        // Act
        var getSoloResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/event-types");
        getSoloResponse.EnsureSuccessStatusCode();
        var soloEventTypes = (await getSoloResponse.DeserializeResponse<EventTypesResponse>())!;

        var getTeamResponse = await teamClient.GetAsync("/api/event-types");
        getTeamResponse.EnsureSuccessStatusCode();
        var teamEventTypes = (await getTeamResponse.DeserializeResponse<EventTypesResponse>())!;

        // Assert
        soloEventTypes.EventTypes.Select(e => e.Id).Should().Contain(soloEventType.Id);
        soloEventTypes.EventTypes.Select(e => e.Id).Should().NotContain(teamEventType.Id);

        teamEventTypes.EventTypes.Select(e => e.Id).Should().Contain(teamEventType.Id);
        teamEventTypes.EventTypes.Select(e => e.Id).Should().NotContain(soloEventType.Id);
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

    private static object NewEventTypeRequest(string scheduleId, string title, string slug)
    {
        return new
        {
            title,
            slug,
            durationMinutes = 30,
            hidden = false,
            scheduleId,
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 60,
            locationType = "link",
            locationValue = "https://example.com/meet"
        };
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record SchedulesResponse(ScheduleResponse[] Schedules);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id, string Name);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypesResponse(EventTypeResponse[] EventTypes);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(string Id, string Title);
}

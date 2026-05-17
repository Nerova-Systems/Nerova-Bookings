using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Scheduling;

public sealed class PublicSchedulingEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task UpdateSchedulingProfile_WhenHandleIsReserved_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new { handle = "api", displayName = "Owner", avatarUrl = (string?)null };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/scheduling/profile", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Scheduling handle 'api' is reserved.");
    }

    [Fact]
    public async Task UpdateSchedulingProfile_WhenHandleAlreadyExists_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");

        var memberResponse = await AuthenticatedMemberHttpClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle = "owner", displayName = "Member", avatarUrl = (string?)null }
        );

        // Assert
        await memberResponse.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Scheduling handle 'owner' is already taken.");
    }

    [Fact]
    public async Task GetPublicEventType_WhenEventTypeIsVisible_ShouldReturnPublicDetails()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/event-types/owner/intro-call");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var eventType = await response.DeserializeResponse<PublicEventTypeResponse>();
        eventType!.Handle.Should().Be("owner");
        eventType.Slug.Should().Be("intro-call");
        eventType.Title.Should().Be("Intro call");
        eventType.Profile.DisplayName.Should().Be("Owner Name");
        eventType.DurationOptions.Should().Equal(30);
    }

    [Fact]
    public async Task GetPublicEventType_WhenEventTypeIsHiddenWithoutPrivateLink_ShouldReturnNotFound()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Private call", "private-call", true, new { privateLinks = new[] { "vip" } });

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/event-types/owner/private-call");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, "Public event type 'owner/private-call' was not found.");
    }

    [Fact]
    public async Task GetPublicEventType_WhenEventTypeIsHiddenWithPrivateLink_ShouldReturnPublicDetails()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Private call", "private-call", true, new { privateLinks = new[] { "vip" } });

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/event-types/owner/private-call?privateLink=vip");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var eventType = await response.DeserializeResponse<PublicEventTypeResponse>();
        eventType!.Title.Should().Be("Private call");
    }

    [Fact]
    public async Task GetPublicSlots_WhenScheduleHasAvailability_ShouldReturnSlotsInRequestedTimeZone()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/slots?handle=owner&eventSlug=intro-call&startTime=2026-06-01T00:00:00Z&endTime=2026-06-02T00:00:00Z&timeZone=Africa/Johannesburg&duration=30");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        slots!.Slots["2026-06-01"].Select(slot => slot.Time).Should().Contain(DateTimeOffset.Parse("2026-06-01T07:00:00Z"));
        slots.Slots["2026-06-01"].Select(slot => slot.EndTime).Should().Contain(DateTimeOffset.Parse("2026-06-01T07:30:00Z"));
    }

    [Fact]
    public async Task GetPublicSlots_WhenStoredDurationOptionsAreEmpty_ShouldUsePrimaryDuration()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await ReplaceEventTypeSettingsAsync(eventType.Id, """{"DurationOptions":[]}""");

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/slots?handle=owner&eventSlug=intro-call&startTime=2026-06-01T00:00:00Z&endTime=2026-06-02T00:00:00Z&timeZone=Africa/Johannesburg&duration=30");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        slots!.Slots["2026-06-01"].Select(slot => slot.Time).Should().Contain(DateTimeOffset.Parse("2026-06-01T07:00:00Z"));
    }

    [Fact]
    public async Task CreatePublicBooking_WhenSlotIsAlreadyBooked_ShouldReturnConflict()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = new
        {
            handle = "owner",
            eventSlug = "intro-call",
            startTime = "2026-06-01T07:00:00Z",
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Ada Lovelace",
            bookerEmail = "ada@example.com",
            responses = new Dictionary<string, string> { ["topic"] = "Scheduling" }
        };
        var firstResponse = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);
        firstResponse.EnsureSuccessStatusCode();

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Conflict, "The selected slot is no longer available.");
    }

    private async Task<SchedulingProfileResponse> UpdateSchedulingProfileAsync(string handle)
    {
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            "/api/scheduling/profile",
            new { handle, displayName = "Owner Name", avatarUrl = "https://example.com/avatar.png" }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<SchedulingProfileResponse>())!;
    }

    private async Task<ScheduleResponse> CreateScheduleAsync()
    {
        var command = new
        {
            name = "Working hours",
            timeZone = "Africa/Johannesburg",
            isDefault = true,
            availabilityWindows = new[]
            {
                new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 }
            },
            dateOverrides = Array.Empty<object>()
        };

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", command);
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleResponse>())!;
    }

    private async Task<EventTypeResponse> CreateEventTypeAsync(string scheduleId, string title, string slug, bool hidden = false, object? settings = null)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/event-types",
            new
            {
                title,
                slug,
                description = "A short consultation",
                durationMinutes = 30,
                hidden,
                scheduleId,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 0,
                locationType = "link",
                locationValue = "https://example.com/meet",
                settings
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeResponse>())!;
    }

    private async Task ReplaceEventTypeSettingsAsync(string eventTypeId, string settings)
    {
        using var serviceScope = Provider.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<MainDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("UPDATE event_types SET settings = {0} WHERE id = {1}", settings, eventTypeId);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record SchedulingProfileResponse(string Handle, string DisplayName, string? AvatarUrl);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicEventTypeResponse(
        string Handle,
        string Slug,
        string Title,
        int[] DurationOptions,
        PublicSchedulingProfileResponse Profile
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicSchedulingProfileResponse(string DisplayName, string? AvatarUrl);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicSlotsResponse(Dictionary<string, PublicSlotResponse[]> Slots);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record PublicSlotResponse(DateTimeOffset Time, DateTimeOffset EndTime);
}

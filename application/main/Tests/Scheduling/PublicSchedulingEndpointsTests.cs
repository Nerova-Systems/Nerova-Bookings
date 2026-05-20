using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
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
    public async Task GetSchedulingProfile_WhenDefaultHandleExistsOutsideTenant_ShouldCreateSuffixedHandle()
    {
        // Arrange
        using (var serviceScope = Provider.CreateScope())
        {
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<MainDbContext>();
            dbContext.Set<SchedulingProfile>().Add(
                SchedulingProfile.Create(
                    new TenantId(DatabaseSeeder.TenantId.Value + 1),
                    UserId.NewId(),
                    "owner",
                    "External Owner",
                    null
                )
            );
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/scheduling/profile");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var profile = await response.DeserializeResponse<SchedulingProfileResponse>();
        profile!.Handle.Should().Be("owner-2");
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
    public async Task GetPublicEventType_WhenPrivateLinkIsExpired_ShouldReturnNotFound()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(
            schedule.Id,
            "Private call",
            "private-call",
            true,
            new { privateLinks = new[] { new { link = "vip", expiresAt = "2026-05-01T00:00:00Z", maxUsageCount = (int?)null, usageCount = 0 } } }
        );

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/event-types/owner/private-call?privateLink=vip");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, "Public event type 'owner/private-call' was not found.");
    }

    [Fact]
    public async Task GetPublicEventType_WhenPrivateLinkUsageLimitIsReached_ShouldReturnNotFound()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(
            schedule.Id,
            "Private call",
            "private-call",
            true,
            new { privateLinks = new[] { new { link = "vip", expiresAt = (string?)null, maxUsageCount = 1, usageCount = 1 } } }
        );

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/event-types/owner/private-call?privateLink=vip");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, "Public event type 'owner/private-call' was not found.");
    }

    [Fact]
    public async Task CreatePublicBooking_WhenPrivateLinkHasUsageLimit_ShouldConsumePrivateLink()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(
            schedule.Id,
            "Private call",
            "private-call",
            true,
            new { privateLinks = new[] { new { link = "vip", expiresAt = "2026-06-30T23:59:59Z", maxUsageCount = 1, usageCount = 0 } } }
        );
        var command = new
        {
            handle = "owner",
            eventSlug = "private-call",
            startTime = "2026-06-01T07:00:00Z",
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Ada Lovelace",
            bookerEmail = "ada@example.com",
            privateLink = "vip",
            responses = new Dictionary<string, string>()
        };

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var lookupResponse = await AnonymousHttpClient.GetAsync("/api/public/event-types/owner/private-call?privateLink=vip");
        await lookupResponse.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, "Public event type 'owner/private-call' was not found.");
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
    public async Task GetPublicSlots_WhenGoogleSelectedCalendarHasBusyWindow_ShouldRemoveBusySlot()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(
            schedule.Id,
            "Intro call",
            "intro-call",
            settings: new
            {
                selectedCalendars = new[]
                {
                    new
                    {
                        integration = "google-calendar",
                        externalId = "primary",
                        credentialId = "fake-busy:owner-scope|2026-06-01T07:00:00Z/2026-06-01T07:30:00Z"
                    }
                }
            }
        );

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/slots?handle=owner&eventSlug=intro-call&startTime=2026-06-01T00:00:00Z&endTime=2026-06-02T00:00:00Z&timeZone=Africa/Johannesburg&duration=30");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        slots!.Slots["2026-06-01"].Select(slot => slot.Time).Should().NotContain(DateTimeOffset.Parse("2026-06-01T07:00:00Z"));
        slots.Slots["2026-06-01"].Select(slot => slot.Time).Should().Contain(DateTimeOffset.Parse("2026-06-01T07:30:00Z"));
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

    [Fact]
    public async Task CreatePublicBooking_WhenMaxActiveBookingsPerBookerReached_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", settings: new { limits = new { maxActiveBookingsPerBooker = 1 } });
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        var command = new
        {
            handle = "owner",
            eventSlug = "intro-call",
            startTime = "2026-06-01T08:00:00Z",
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Ada Lovelace",
            bookerEmail = "ada@example.com",
            responses = new Dictionary<string, string>()
        };

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "You already have the maximum number of active bookings for this event type.");
    }

    [Fact]
    public async Task CreatePublicBooking_WhenDailyBookingLimitReached_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", settings: new { limits = new { maxBookingsPerDay = 1 } });
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        var command = new
        {
            handle = "owner",
            eventSlug = "intro-call",
            startTime = "2026-06-01T08:00:00Z",
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Grace Hopper",
            bookerEmail = "grace@example.com",
            responses = new Dictionary<string, string>()
        };

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "This event type has reached its booking limit for the selected day.");
    }

    [Fact]
    public async Task CreatePublicBooking_WhenDailyDurationLimitReached_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call", settings: new { limits = new { maxBookingDurationMinutesPerDay = 45 } });
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        var command = new
        {
            handle = "owner",
            eventSlug = "intro-call",
            startTime = "2026-06-01T08:00:00Z",
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Grace Hopper",
            bookerEmail = "grace@example.com",
            responses = new Dictionary<string, string>()
        };

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "This event type has reached its booking duration limit for the selected day.");
    }

    [Fact]
    public async Task CreatePublicBooking_WhenBookingFieldOptionIsInvalid_ShouldReturnBadRequest()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(
            schedule.Id,
            "Intro call",
            "intro-call",
            settings: new
            {
                bookingFields = new[]
                {
                    new { name = "topic", label = "Topic", type = "select", required = true, options = new[] { "Sales", "Support" } }
                }
            }
        );

        var command = new
        {
            handle = "owner",
            eventSlug = "intro-call",
            startTime = "2026-06-01T07:00:00Z",
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Ada Lovelace",
            bookerEmail = "ada@example.com",
            responses = new Dictionary<string, string> { ["topic"] = "Engineering" }
        };

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Topic is not a valid option.");
    }

    [Fact]
    public async Task CreatePublicBooking_WhenBookingFieldUsesCalCompatibleOptions_ShouldValidateAgainstOptionValues()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(
            schedule.Id,
            "Intro call",
            "intro-call",
            settings: new
            {
                bookingFields = new[]
                {
                    new
                    {
                        name = "topic",
                        label = "Topic",
                        type = "select",
                        required = true,
                        options = new[] { new { label = "Sales", value = "sales" }, new { label = "Support", value = "support" } }
                    }
                }
            }
        );
        var command = new
        {
            handle = "owner",
            eventSlug = "intro-call",
            startTime = "2026-06-01T07:00:00Z",
            duration = 30,
            timeZone = "Africa/Johannesburg",
            bookerName = "Ada Lovelace",
            bookerEmail = "ada@example.com",
            responses = new Dictionary<string, string> { ["topic"] = "sales" }
        };

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/public/bookings", command);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetPublicSlots_WhenSeatsAreEnabled_ShouldHideOverlappingSlotsAtCapacity()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(
            schedule.Id,
            "Intro call",
            "intro-call",
            slotIntervalMinutes: 15,
            settings: new { seats = new { enabled = true, capacity = 1, showAttendeeInfo = false } }
        );
        await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z", "Ada Lovelace", "ada@example.com");

        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/public/slots?handle=owner&eventSlug=intro-call&startTime=2026-06-01T00:00:00Z&endTime=2026-06-02T00:00:00Z&timeZone=Africa/Johannesburg&duration=30");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var slots = await response.DeserializeResponse<PublicSlotsResponse>();
        slots!.Slots["2026-06-01"].Select(slot => slot.Time).Should().NotContain(DateTimeOffset.Parse("2026-06-01T07:00:00Z"));
        slots.Slots["2026-06-01"].Select(slot => slot.Time).Should().NotContain(DateTimeOffset.Parse("2026-06-01T07:15:00Z"));
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

    private async Task<EventTypeResponse> CreateEventTypeAsync(string scheduleId, string title, string slug, bool hidden = false, object? settings = null, int slotIntervalMinutes = 30)
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
                slotIntervalMinutes,
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

    private async Task<CreatePublicBookingResponse> CreateBookingAsync(string eventSlug, string startTime, string bookerName, string bookerEmail)
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/public/bookings",
            new
            {
                handle = "owner",
                eventSlug,
                startTime,
                duration = 30,
                timeZone = "Africa/Johannesburg",
                bookerName,
                bookerEmail,
                responses = new Dictionary<string, string>()
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<CreatePublicBookingResponse>())!;
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

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record CreatePublicBookingResponse(string Id);
}

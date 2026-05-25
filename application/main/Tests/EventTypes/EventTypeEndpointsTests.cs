using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
using SharedKernel.Validation;
using Xunit;

namespace Main.Tests.EventTypes;

public sealed class EventTypeEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task CreateEventType_WhenOwnerCreatesEventType_ShouldPersistAndReturnEventType()
    {
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(schedule.Id, "Intro call", "intro-call");

        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", command);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.DeserializeResponse<EventTypeResponse>();

        created.Should().NotBeNull();
        created.Title.Should().Be("Intro call");
        created.Slug.Should().Be("intro-call");
        created.DurationMinutes.Should().Be(30);
        created.ScheduleId.Should().Be(schedule.Id);
        created.LocationType.Should().Be("link");
        created.LocationValue.Should().Be("https://example.com/meet");

        var getResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{created.Id}");
        getResponse.ShouldBeSuccessfulGetRequest();
        var fetched = await getResponse.DeserializeResponse<EventTypeResponse>();
        fetched!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task CreateEventType_WhenSettingsAreProvided_ShouldPersistAndReturnSettings()
    {
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(
            schedule.Id,
            "Team workshop",
            "team-workshop",
            durationMinutes: 60,
            locationType: "link",
            locationValue: "https://example.com/workshop",
            settings: NewSettings()
        );

        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", command);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.DeserializeResponse<EventTypeResponse>();

        created!.Settings.DurationOptions.Should().Equal(30, 60, 90);
        created.Settings.Locations.Should().ContainSingle();
        created.Settings.Locations[0].Type.Should().Be("inPerson");
        created.Settings.Locations[0].Value.Should().Be("Boardroom");
        created.Settings.BookingFields.Should().ContainSingle();
        created.Settings.BookingWindow.FixedStartDate.Should().Be(new DateOnly(2026, 6, 1));
        created.Settings.BookingWindow.FixedEndDate.Should().Be(new DateOnly(2026, 6, 30));
        created.Settings.Limits.MaxBookingsPerDay.Should().Be(4);
        created.Settings.Recurrence!.Interval.Should().Be(2);
        created.Settings.Seats.Enabled.Should().BeFalse();

        var getResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{created.Id}");
        getResponse.ShouldBeSuccessfulGetRequest();
        var fetched = await getResponse.DeserializeResponse<EventTypeResponse>();
        fetched!.Settings.Should().BeEquivalentTo(created.Settings);
    }

    [Fact]
    public async Task CreateEventType_WhenSettingsOmitDurationsAndLocations_ShouldNormalizeFromSimpleFields()
    {
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(
            schedule.Id,
            "Phone consult",
            "phone-consult",
            durationMinutes: 45,
            locationType: "phone",
            locationValue: "+27110000000",
            settings: new { bookingFields = Array.Empty<object>() }
        );

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", command);
        response.EnsureSuccessStatusCode();
        var created = await response.DeserializeResponse<EventTypeResponse>();

        created!.Settings.DurationOptions.Should().Equal(45);
        created.Settings.Locations.Should().ContainSingle();
        created.Settings.Locations[0].Type.Should().Be("phone");
        created.Settings.Locations[0].Value.Should().Be("+27110000000");
    }

    [Fact]
    public async Task CreateEventType_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/event-types", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEventType_WhenPrimitiveFieldsAreInvalid_ShouldReturnValidationErrors()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(
            schedule.Id,
            "",
            "Bad Slug!",
            durationMinutes: 4,
            beforeEventBufferMinutes: -1,
            afterEventBufferMinutes: 1441,
            slotIntervalMinutes: 4,
            minimumBookingNoticeMinutes: -1,
            locationType: new string('t', 81),
            locationValue: new string('v', 501)
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.BadRequest,
            [
                new ErrorDetail("Title", "'Title' must not be empty."),
                new ErrorDetail("Slug", "Slug must contain lowercase letters, numbers, and hyphens only."),
                new ErrorDetail("DurationMinutes", "'Duration Minutes' must be between 5 and 1440. You entered 4."),
                new ErrorDetail("BeforeEventBufferMinutes", "'Before Event Buffer Minutes' must be between 0 and 1440. You entered -1."),
                new ErrorDetail("AfterEventBufferMinutes", "'After Event Buffer Minutes' must be between 0 and 1440. You entered 1441."),
                new ErrorDetail("SlotIntervalMinutes", "'Slot Interval Minutes' must be between 5 and 1440. You entered 4."),
                new ErrorDetail("MinimumBookingNoticeMinutes", "'Minimum Booking Notice Minutes' must be between 0 and 525600. You entered -1."),
                new ErrorDetail("LocationType", "The length of 'Location Type' must be 80 characters or fewer. You entered 81 characters."),
                new ErrorDetail("LocationValue", "The length of 'Location Value' must be 500 characters or fewer. You entered 501 characters.")
            ]
        );
    }

    [Fact]
    public async Task CreateEventType_WhenSettingsAreInvalid_ShouldReturnValidationErrors()
    {
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(
            schedule.Id,
            "Invalid settings",
            "invalid-settings",
            settings: new
            {
                durationOptions = new[] { 4, 30 },
                locations = new[] { new { type = "", value = new string('x', 501) } },
                bookingFields = new[] { new { name = "", label = "", type = "", required = true, options = Array.Empty<string>() } },
                bookingWindow = new { fixedStartDate = "2026-07-01", fixedEndDate = "2026-06-30" },
                limits = new
                {
                    maxBookingsPerDay = -1,
                    maxBookingDurationMinutesPerDay = -1,
                    maxActiveBookingsPerBooker = -1,
                    firstAvailableSlotMinutes = -1,
                    offsetStartMinutes = -1
                },
                recurrence = new { frequency = "weekly", interval = 0, count = 0 },
                seats = new { enabled = true, capacity = 0, showAttendeeInfo = true }
            }
        );

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", command);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.BadRequest,
            [
                new ErrorDetail("Settings.DurationOptions", "Duration options must be between 5 and 1440 minutes."),
                new ErrorDetail("Settings.Locations[0].Type", "Location type must be between 1 and 80 characters."),
                new ErrorDetail("Settings.Locations[0].Value", "Location value must be at most 500 characters."),
                new ErrorDetail("Settings.BookingFields[0].Name", "Booking field name is required."),
                new ErrorDetail("Settings.BookingFields[0].Label", "Booking field label is required."),
                new ErrorDetail("Settings.BookingFields[0].Type", "Booking field type is required."),
                new ErrorDetail("Settings.BookingWindow", "Fixed booking window start date must be before or equal to end date."),
                new ErrorDetail("Settings.Limits", "Event type limits must be non-negative."),
                new ErrorDetail("Settings.Recurrence.Interval", "Recurrence interval must be positive."),
                new ErrorDetail("Settings.Recurrence.Count", "Recurrence count must be positive."),
                new ErrorDetail("Settings.Seats.Capacity", "Seats capacity must be positive when seats are enabled."),
                new ErrorDetail("Settings", "Recurring event types cannot use seats.")
            ]
        );
    }

    [Fact]
    public async Task CreateEventType_WhenMemberCreatesEventType_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/event-types", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners and admins can manage event types.");
    }

    [Fact]
    public async Task GetEventTypes_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync("/api/event-types");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEventTypes_WhenMemberRequestsOwnerEventTypes_ShouldNotReturnOwnerEventTypes()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var ownerEventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/event-types");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var eventTypes = await response.DeserializeResponse<EventTypesResponse>();
        eventTypes!.EventTypes.Select(e => e.Id).Should().NotContain(ownerEventType.Id);
    }

    [Fact]
    public async Task CreateEventType_WhenSlugAlreadyExistsForOwner_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", NewEventTypeRequest(schedule.Id, "Another intro", "intro-call"));

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "An event type with slug 'intro-call' already exists.");
    }

    [Fact]
    public async Task CreateEventType_WhenScheduleDoesNotExist_ShouldReturnBadRequest()
    {
        var command = NewEventTypeRequest("sch_01ARZ3NDEKTSV4RRFFQ69G5FAV", "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Schedule 'sch_01ARZ3NDEKTSV4RRFFQ69G5FAV' was not found.");
    }

    [Fact]
    public async Task GetEventType_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AnonymousHttpClient.GetAsync($"/api/event-types/{eventType.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEventType_WhenMemberRequestsOwnerEventType_ShouldReturnNotFound()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync($"/api/event-types/{eventType.Id}");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"Event type '{eventType.Id}' was not found.");
    }

    [Fact]
    public async Task UpdateEventType_WhenOwnerUpdatesEventType_ShouldReplaceEditableDetails()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = NewEventTypeRequest(
            schedule.Id,
            "Deep dive",
            "deep-dive",
            "Updated description",
            45,
            true,
            10,
            15,
            15,
            120,
            "phone",
            "+27110000000"
        );

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);
        response.EnsureSuccessStatusCode();
        var updated = await response.DeserializeResponse<EventTypeResponse>();

        updated!.Title.Should().Be("Deep dive");
        updated.Slug.Should().Be("deep-dive");
        updated.Description.Should().Be("Updated description");
        updated.DurationMinutes.Should().Be(45);
        updated.Hidden.Should().BeTrue();
        updated.BeforeEventBufferMinutes.Should().Be(10);
        updated.AfterEventBufferMinutes.Should().Be(15);
        updated.SlotIntervalMinutes.Should().Be(15);
        updated.MinimumBookingNoticeMinutes.Should().Be(120);
        updated.LocationType.Should().Be("phone");
        updated.LocationValue.Should().Be("+27110000000");
    }

    [Fact]
    public async Task UpdateEventType_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = NewEventTypeRequest(schedule.Id, "Deep dive", "deep-dive");

        // Act
        var response = await AnonymousHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateEventType_WhenMemberUpdatesOwnerEventType_ShouldReturnForbidden()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = NewEventTypeRequest(schedule.Id, "Deep dive", "deep-dive");

        // Act
        var response = await AuthenticatedMemberHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners and admins can manage event types.");
    }

    [Fact]
    public async Task UpdateEventType_WhenSettingsAreProvided_ShouldPersistAndReturnSettings()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = NewEventTypeRequest(
            schedule.Id,
            "Team workshop",
            "team-workshop",
            durationMinutes: 60,
            locationType: "link",
            locationValue: "https://example.com/workshop",
            settings: NewSettings()
        );

        var updateResponse = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.DeserializeResponse<EventTypeResponse>();

        updated!.Settings.DurationOptions.Should().Equal(30, 60, 90);
        updated.Settings.Locations.Should().ContainSingle();
        updated.Settings.Locations[0].Type.Should().Be("inPerson");
        updated.Settings.BookingFields.Should().ContainSingle();
        updated.Settings.BookerLayout.Should().Be("week");
        updated.Settings.EventColor.Should().Be("#2f6fed");
        updated.Settings.PrivateLinks.Should().Equal("vip");
        updated.Settings.Redirects.SuccessUrl.Should().Be("https://example.com/success");
        updated.Settings.InterfaceLanguage.Should().Be("en");
        updated.Settings.Metadata.Should().Contain("source", "test");

        var getResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{updated.Id}");
        getResponse.ShouldBeSuccessfulGetRequest();
        var fetched = await getResponse.DeserializeResponse<EventTypeResponse>();
        fetched!.Settings.Should().BeEquivalentTo(updated.Settings);
    }

    [Fact]
    public async Task UpdateEventType_WhenSettingsAreSemanticallyInvalid_ShouldReturnValidationErrors()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = NewEventTypeRequest(
            schedule.Id,
            "Invalid settings",
            "invalid-settings",
            settings: new
            {
                durationOptions = new[] { 0, 30 },
                locations = new[] { new { type = "ftp", value = "x" } },
                bookingFields = new[]
                {
                    new { name = "topic", label = "Topic", type = "select", required = true, options = Array.Empty<string>() },
                    new { name = "legacy", label = "Legacy", type = "script", required = false, options = Array.Empty<string>() }
                },
                bookerLayout = "agenda",
                eventColor = "blue",
                recurrence = new { frequency = "hourly", interval = 1, count = 1 },
                privateLinks = new[] { "contains spaces" },
                redirects = new { successUrl = "ftp://example.com/success", cancellationUrl = "/cancel" },
                interfaceLanguage = "english!",
                metadata = new Dictionary<string, string> { [new string('k', 81)] = new('v', 501) }
            }
        );

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.BadRequest,
            [
                new ErrorDetail("Settings.DurationOptions", "Duration options must be between 5 and 1440 minutes."),
                new ErrorDetail("Settings.Locations[0].Type", "Location type is not supported."),
                new ErrorDetail("Settings.BookingFields[0].Options", "Booking field options are required for this field type."),
                new ErrorDetail("Settings.BookingFields[1].Type", "Booking field type is not supported."),
                new ErrorDetail("Settings.BookerLayout", "Booker layout must be month, week, or column."),
                new ErrorDetail("Settings.EventColor", "Event color must be a valid hex color."),
                new ErrorDetail("Settings.Recurrence.Frequency", "Recurrence frequency must be daily, weekly, monthly, or yearly."),
                new ErrorDetail("Settings.PrivateLinks[0]", "Private link must contain only letters, numbers, underscores, and hyphens."),
                new ErrorDetail("Settings.Redirects.SuccessUrl", "Success redirect URL must be an absolute HTTP or HTTPS URL."),
                new ErrorDetail("Settings.Redirects.CancellationUrl", "Cancellation redirect URL must be an absolute HTTP or HTTPS URL."),
                new ErrorDetail("Settings.InterfaceLanguage", "Interface language must be a valid language tag."),
                new ErrorDetail("Settings.Metadata", "Metadata keys must be at most 80 characters and values must be at most 500 characters.")
            ]
        );
    }

    [Fact]
    public async Task UpdateEventType_WhenScheduleDoesNotExist_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = NewEventTypeRequest("sch_01ARZ3NDEKTSV4RRFFQ69G5FAV", "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Schedule 'sch_01ARZ3NDEKTSV4RRFFQ69G5FAV' was not found.");
    }

    [Fact]
    public async Task UpdateEventType_WhenSlugAlreadyExistsForOwner_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync();
        var existing = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var eventType = await CreateEventTypeAsync(schedule.Id, "Deep dive", "deep-dive");
        var command = NewEventTypeRequest(schedule.Id, eventType.Title, existing.Slug);

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "An event type with slug 'intro-call' already exists.");
    }

    [Fact]
    public async Task UpdateEventType_WhenPrimitiveFieldsAreInvalid_ShouldReturnValidationErrors()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var command = NewEventTypeRequest(
            schedule.Id,
            new string('t', 121),
            "Bad Slug!",
            durationMinutes: 1441,
            beforeEventBufferMinutes: -1,
            afterEventBufferMinutes: 1441,
            slotIntervalMinutes: 1441,
            minimumBookingNoticeMinutes: 525601,
            locationType: new string('t', 81),
            locationValue: new string('v', 501)
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/event-types/{eventType.Id}", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.BadRequest,
            [
                new ErrorDetail("Title", "The length of 'Title' must be 120 characters or fewer. You entered 121 characters."),
                new ErrorDetail("Slug", "Slug must contain lowercase letters, numbers, and hyphens only."),
                new ErrorDetail("DurationMinutes", "'Duration Minutes' must be between 5 and 1440. You entered 1441."),
                new ErrorDetail("BeforeEventBufferMinutes", "'Before Event Buffer Minutes' must be between 0 and 1440. You entered -1."),
                new ErrorDetail("AfterEventBufferMinutes", "'After Event Buffer Minutes' must be between 0 and 1440. You entered 1441."),
                new ErrorDetail("SlotIntervalMinutes", "'Slot Interval Minutes' must be between 5 and 1440. You entered 1441."),
                new ErrorDetail("MinimumBookingNoticeMinutes", "'Minimum Booking Notice Minutes' must be between 0 and 525600. You entered 525601."),
                new ErrorDetail("LocationType", "The length of 'Location Type' must be 80 characters or fewer. You entered 81 characters."),
                new ErrorDetail("LocationValue", "The length of 'Location Value' must be 500 characters or fewer. You entered 501 characters.")
            ]
        );
    }

    [Fact]
    public async Task DeleteEventType_WhenOwnerDeletesEventType_ShouldRemoveEventType()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var deleteResponse = await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/event-types/{eventType.Id}");
        deleteResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{eventType.Id}");
        await getResponse.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"Event type '{eventType.Id}' was not found.");
    }

    [Fact]
    public async Task DeleteEventType_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AnonymousHttpClient.DeleteAsync($"/api/event-types/{eventType.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteEventType_WhenMemberDeletesOwnerEventType_ShouldReturnForbidden()
    {
        // Arrange
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        // Act
        var response = await AuthenticatedMemberHttpClient.DeleteAsync($"/api/event-types/{eventType.Id}");

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "You do not have permission to perform this action.");
    }

    [Fact]
    public async Task GetEventTypes_WhenOwnerHasEventTypes_ShouldReturnEventTypesOrderedByTitle()
    {
        var schedule = await CreateScheduleAsync();
        var later = await CreateEventTypeAsync(schedule.Id, "Zoom consult", "zoom-consult");
        var earlier = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/event-types");
        response.ShouldBeSuccessfulGetRequest();
        var eventTypes = await response.DeserializeResponse<EventTypesResponse>();

        eventTypes!.EventTypes.Select(e => e.Id).Should().Equal(earlier.Id, later.Id);
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
            }
        };

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", command);
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleResponse>())!;
    }

    private async Task<EventTypeResponse> CreateEventTypeAsync(string scheduleId, string title, string slug)
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", NewEventTypeRequest(scheduleId, title, slug));
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeResponse>())!;
    }

    private static object NewEventTypeRequest(
        string scheduleId,
        string title,
        string slug,
        string? description = "A short consultation",
        int durationMinutes = 30,
        bool hidden = false,
        int beforeEventBufferMinutes = 0,
        int afterEventBufferMinutes = 0,
        int slotIntervalMinutes = 30,
        int minimumBookingNoticeMinutes = 60,
        string? locationType = "link",
        string? locationValue = "https://example.com/meet",
        object? settings = null
    )
    {
        return new
        {
            title,
            slug,
            description,
            durationMinutes,
            hidden,
            scheduleId,
            beforeEventBufferMinutes,
            afterEventBufferMinutes,
            slotIntervalMinutes,
            minimumBookingNoticeMinutes,
            locationType,
            locationValue,
            settings
        };
    }

    private static object NewSettings()
    {
        return new
        {
            durationOptions = new[] { 90, 30, 60, 60 },
            locations = new[] { new { type = "inPerson", value = "Boardroom" } },
            bookingFields = new[]
            {
                new { name = "company", label = "Company", type = "text", required = true, options = Array.Empty<string>() }
            },
            bookerLayout = "week",
            eventColor = "#2f6fed",
            bookingWindow = new { rollingWindowDays = 30, fixedStartDate = "2026-06-01", fixedEndDate = "2026-06-30" },
            limits = new { maxBookingsPerDay = 4, maxBookingDurationMinutesPerDay = 240, maxActiveBookingsPerBooker = 2, firstAvailableSlotMinutes = 15, offsetStartMinutes = 10 },
            confirmationPolicy = new { requiresConfirmation = true, requiresBookerEmailVerification = true },
            recurrence = new { frequency = "weekly", interval = 2, count = 5 },
            seats = new { enabled = false, capacity = (int?)null, showAttendeeInfo = false },
            privateLinks = new[] { " vip ", "VIP" },
            cancellationPolicy = new { allowCancellation = true, minimumNoticeMinutes = 120 },
            reschedulePolicy = new { allowReschedule = true, minimumNoticeMinutes = 180 },
            redirects = new { successUrl = "https://example.com/success", cancellationUrl = "https://example.com/cancel" },
            interfaceLanguage = "en",
            metadata = new Dictionary<string, string> { ["source"] = "test" }
        };
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypesResponse(EventTypeResponse[] EventTypes);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(
        string Id,
        string Title,
        string Slug,
        string? Description,
        int DurationMinutes,
        bool Hidden,
        string ScheduleId,
        int BeforeEventBufferMinutes,
        int AfterEventBufferMinutes,
        int SlotIntervalMinutes,
        int MinimumBookingNoticeMinutes,
        string? LocationType,
        string? LocationValue,
        EventTypeSettingsResponse Settings
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeSettingsResponse(
        int[] DurationOptions,
        EventTypeLocationResponse[] Locations,
        EventTypeBookingFieldResponse[] BookingFields,
        string BookerLayout,
        string? EventColor,
        EventTypeBookingWindowResponse BookingWindow,
        EventTypeLimitsResponse Limits,
        EventTypeConfirmationPolicyResponse ConfirmationPolicy,
        EventTypeRecurrenceResponse? Recurrence,
        EventTypeSeatsResponse Seats,
        string[] PrivateLinks,
        EventTypeCancellationPolicyResponse CancellationPolicy,
        EventTypeReschedulePolicyResponse ReschedulePolicy,
        EventTypeRedirectsResponse Redirects,
        string? InterfaceLanguage,
        Dictionary<string, string> Metadata
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeLocationResponse(string Type, string? Value);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeBookingFieldResponse(string Name, string Label, string Type, bool Required, string[] Options);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeBookingWindowResponse(int? RollingWindowDays, DateOnly? FixedStartDate, DateOnly? FixedEndDate);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeLimitsResponse(int? MaxBookingsPerDay, int? MaxBookingDurationMinutesPerDay, int? MaxActiveBookingsPerBooker, int? FirstAvailableSlotMinutes, int? OffsetStartMinutes);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeConfirmationPolicyResponse(bool RequiresConfirmation, bool RequiresBookerEmailVerification);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeRecurrenceResponse(string Frequency, int Interval, int? Count);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeSeatsResponse(bool Enabled, int? Capacity, bool ShowAttendeeInfo);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeCancellationPolicyResponse(bool AllowCancellation, int? MinimumNoticeMinutes);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeReschedulePolicyResponse(bool AllowReschedule, int? MinimumNoticeMinutes);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeRedirectsResponse(string? SuccessUrl, string? CancellationUrl);

    [Fact]
    public async Task GetEventTypesByViewer_WhenOwnerHasEventTypes_ShouldReturnPersonalGroup()
    {
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/event-types/by-viewer");
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<EventTypesByViewerResponse>();

        result!.Groups.Should().ContainSingle(group => group.Kind == "personal");
        result.Groups[0].EventTypes.Should().ContainSingle(eventType => eventType.Slug == "intro-call");
    }

    [Fact]
    public async Task GetEventTypesByViewer_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync("/api/event-types/by-viewer");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEventTypeGroups_WhenOwnerHasEventTypes_ShouldReturnPersonalGroupCount()
    {
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        await CreateEventTypeAsync(schedule.Id, "Zoom consult", "zoom-consult");

        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/event-types/groups");
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<EventTypeGroupsResponse>();

        result!.Groups.Should().ContainSingle(group => group.Kind == "personal" && group.Count == 2);
    }

    [Fact]
    public async Task GetHostsForAssignment_WhenSoloEventType_ShouldReturnOwnerOnly()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{eventType.Id}/assignment-candidates");
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<HostsForAssignmentResponse>();

        result!.Candidates.Should().ContainSingle();
    }

    [Fact]
    public async Task GetHostsForAssignment_WhenMemberRequestsOwnerEventType_ShouldReturnNotFound()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedMemberHttpClient.GetAsync($"/api/event-types/{eventType.Id}/assignment-candidates");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"Event type '{eventType.Id}' was not found.");
    }

    [Fact]
    public async Task GetHostsForAvailability_WhenScheduleHasWindows_ShouldReturnScheduleWindowsForOwner()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{eventType.Id}/availability?from=2026-01-05&to=2026-01-11");
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<HostsForAvailabilityResponse>();

        result!.Hosts.Should().ContainSingle();
        result.Hosts[0].TimeZone.Should().Be("Africa/Johannesburg");
        result.Hosts[0].AvailabilityWindows.Should().ContainSingle(window => window.StartMinute == 540 && window.EndMinute == 1020);
    }

    [Fact]
    public async Task GetHostsForAvailability_WhenFromAfterTo_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{eventType.Id}/availability?from=2026-02-10&to=2026-02-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkApplyLocations_WhenOwnerAppliesValidLocations_ShouldPersistAllChanges()
    {
        var schedule = await CreateScheduleAsync();
        var first = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");
        var second = await CreateEventTypeAsync(schedule.Id, "Zoom consult", "zoom-consult");

        var request = new
        {
            items = new[]
            {
                new { eventTypeId = first.Id, locationType = "phone", locationValue = "+1-555-0100" },
                new { eventTypeId = second.Id, locationType = "phone", locationValue = "+1-555-0101" }
            }
        };

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types/bulk-apply-locations", request);
        response.EnsureSuccessStatusCode();

        var refreshedFirst = await (await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{first.Id}")).DeserializeResponse<EventTypeResponse>();
        var refreshedSecond = await (await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{second.Id}")).DeserializeResponse<EventTypeResponse>();
        refreshedFirst!.LocationType.Should().Be("phone");
        refreshedFirst.LocationValue.Should().Be("+1-555-0100");
        refreshedSecond!.LocationType.Should().Be("phone");
        refreshedSecond.LocationValue.Should().Be("+1-555-0101");
    }

    [Fact]
    public async Task BulkApplyLocations_WhenMember_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var eventType = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var request = new
        {
            items = new[] { new { eventTypeId = eventType.Id, locationType = "phone", locationValue = "+1-555-0100" } }
        };

        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/event-types/bulk-apply-locations", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BulkApplyLocations_WhenAnyItemNotFound_ShouldAbortEntireBatch()
    {
        var schedule = await CreateScheduleAsync();
        var first = await CreateEventTypeAsync(schedule.Id, "Intro call", "intro-call");

        var unknownId = Main.Features.EventTypes.Domain.EventTypeId.NewId().Value;
        var request = new
        {
            items = new[]
            {
                new { eventTypeId = first.Id, locationType = "phone", locationValue = "+1-555-0100" },
                new { eventTypeId = unknownId, locationType = "phone", locationValue = "+1-555-0101" }
            }
        };

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types/bulk-apply-locations", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify the first item was NOT persisted (atomic rollback).
        var refreshed = await (await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{first.Id}")).DeserializeResponse<EventTypeResponse>();
        refreshed!.LocationType.Should().Be("link");
        refreshed.LocationValue.Should().Be("https://example.com/meet");
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypesByViewerResponse(EventTypeGroupResponse[] Groups);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeGroupResponse(string Kind, string? TeamId, EventTypeResponse[] EventTypes);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeGroupsResponse(EventTypeGroupSummaryResponse[] Groups);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeGroupSummaryResponse(string Kind, string? TeamId, int Count);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record HostsForAssignmentResponse(HostCandidateResponse[] Candidates);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record HostCandidateResponse(string UserId);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record HostsForAvailabilityResponse(HostAvailabilityResponse[] Hosts);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record HostAvailabilityResponse(string UserId, string TimeZone, HostAvailabilityWindowResponse[] AvailabilityWindows);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record HostAvailabilityWindowResponse(int[] Days, int StartMinute, int EndMinute);
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
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
    public async Task CreateEventType_WhenMemberCreatesEventType_ShouldReturnForbidden()
    {
        var schedule = await CreateScheduleAsync();
        var command = NewEventTypeRequest(schedule.Id, "Intro call", "intro-call");

        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/event-types", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners and admins can manage event types.");
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
        string? locationValue = "https://example.com/meet"
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
            locationValue
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
        string? LocationValue
    );

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);
}

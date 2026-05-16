using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
using SharedKernel.Validation;
using Xunit;

namespace Main.Tests.Schedules;

public sealed class ScheduleEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task CreateSchedule_WhenOwnerCreatesDefaultSchedule_ShouldPersistAndReturnDefaultSchedule()
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

        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", command);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.DeserializeResponse<ScheduleResponse>();

        created.Should().NotBeNull();
        created.Name.Should().Be("Working hours");
        created.TimeZone.Should().Be("Africa/Johannesburg");
        created.IsDefault.Should().BeTrue();
        created.AvailabilityWindows.Should().ContainSingle();
        created.AvailabilityWindows[0].Days.Should().BeEquivalentTo([1, 2, 3, 4, 5]);

        var defaultResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/schedules/default");
        defaultResponse.ShouldBeSuccessfulGetRequest();
        var defaultSchedule = await defaultResponse.DeserializeResponse<ScheduleResponse>();
        defaultSchedule!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task CreateSchedule_WhenOwnerCreatesFirstScheduleAsNonDefault_ShouldPersistItAsDefaultSchedule()
    {
        var command = new
        {
            name = "Working hours",
            timeZone = "Africa/Johannesburg",
            isDefault = false,
            availabilityWindows = new[]
            {
                new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 }
            }
        };

        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", command);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.DeserializeResponse<ScheduleResponse>();

        created!.IsDefault.Should().BeTrue();

        var defaultResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/schedules/default");
        defaultResponse.ShouldBeSuccessfulGetRequest();
        var defaultSchedule = await defaultResponse.DeserializeResponse<ScheduleResponse>();
        defaultSchedule!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task CreateSchedule_WhenMemberCreatesSchedule_ShouldReturnForbidden()
    {
        var command = new
        {
            name = "Working hours",
            timeZone = "Africa/Johannesburg",
            isDefault = true,
            availabilityWindows = Array.Empty<object>()
        };

        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/schedules", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners and admins can manage schedules.");
    }

    [Fact]
    public async Task CreateSchedule_WhenAvailabilityWindowOverlaps_ShouldReturnValidationError()
    {
        var command = new
        {
            name = "Working hours",
            timeZone = "Africa/Johannesburg",
            isDefault = true,
            availabilityWindows = new[]
            {
                new { days = new[] { 1 }, startMinute = 540, endMinute = 720 },
                new { days = new[] { 1 }, startMinute = 660, endMinute = 780 }
            }
        };

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", command);

        await response.ShouldHaveErrorStatusCode(
            HttpStatusCode.BadRequest,
            [new ErrorDetail("availabilityWindows", "Availability windows cannot overlap on the same day.")]
        );
    }

    [Fact]
    public async Task CreateSchedule_WhenSecondDefaultScheduleIsCreated_ShouldMakePreviousScheduleNonDefault()
    {
        var first = await CreateScheduleAsync("First schedule", true);
        var second = await CreateScheduleAsync("Second schedule", true);

        var listResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/schedules");
        listResponse.ShouldBeSuccessfulGetRequest();
        var schedules = await listResponse.DeserializeResponse<SchedulesResponse>();

        schedules!.Schedules.Single(s => s.Id == first.Id).IsDefault.Should().BeFalse();
        schedules.Schedules.Single(s => s.Id == second.Id).IsDefault.Should().BeTrue();

        var defaultResponse = await AuthenticatedOwnerHttpClient.GetAsync("/api/schedules/default");
        var defaultSchedule = await defaultResponse.DeserializeResponse<ScheduleResponse>();
        defaultSchedule!.Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task UpdateSchedule_WhenOwnerUpdatesSchedule_ShouldReplaceScheduleDetails()
    {
        var schedule = await CreateScheduleAsync("Working hours", true);
        var command = new
        {
            name = "Support hours",
            timeZone = "Europe/Copenhagen",
            isDefault = true,
            availabilityWindows = new[]
            {
                new { days = new[] { 2, 4 }, startMinute = 600, endMinute = 900 }
            }
        };

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/schedules/{schedule.Id}", command);
        response.EnsureSuccessStatusCode();
        var updated = await response.DeserializeResponse<ScheduleResponse>();

        updated!.Name.Should().Be("Support hours");
        updated.TimeZone.Should().Be("Europe/Copenhagen");
        updated.AvailabilityWindows.Should().ContainSingle();
        updated.AvailabilityWindows[0].Days.Should().BeEquivalentTo([2, 4]);
        updated.AvailabilityWindows[0].StartMinute.Should().Be(600);
        updated.AvailabilityWindows[0].EndMinute.Should().Be(900);
    }

    [Fact]
    public async Task UpdateSchedule_WhenOnlyDefaultScheduleIsUnset_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync("Working hours", true);
        var command = new
        {
            name = schedule.Name,
            timeZone = schedule.TimeZone,
            isDefault = false,
            availabilityWindows = new[]
            {
                new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 }
            }
        };

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/schedules/{schedule.Id}", command);

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "At least one default schedule is required.");
    }

    [Fact]
    public async Task DeleteSchedule_WhenScheduleIsOnlySchedule_ShouldReturnBadRequest()
    {
        var schedule = await CreateScheduleAsync("Temporary schedule", true);

        var response = await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/schedules/{schedule.Id}");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "At least one schedule is required.");
    }

    [Fact]
    public async Task DeleteSchedule_WhenScheduleIsDefaultSchedule_ShouldReturnBadRequest()
    {
        var defaultSchedule = await CreateScheduleAsync("Default schedule", true);
        await CreateScheduleAsync("Project hours", false);

        var response = await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/schedules/{defaultSchedule.Id}");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Default schedule cannot be deleted. Make another schedule default before deleting it.");
    }

    [Fact]
    public async Task DeleteSchedule_WhenScheduleIsReferencedByEventType_ShouldReturnBadRequest()
    {
        await CreateScheduleAsync("Default schedule", true);
        var schedule = await CreateScheduleAsync("Project hours", false);
        var eventType = new
        {
            title = "Intro call",
            slug = "intro-call",
            description = "A short consultation",
            durationMinutes = 30,
            hidden = false,
            scheduleId = schedule.Id,
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 60,
            locationType = "link",
            locationValue = "https://example.com/meet"
        };
        var createEventTypeResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", eventType);
        createEventTypeResponse.EnsureSuccessStatusCode();

        var response = await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/schedules/{schedule.Id}");

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, $"Schedule '{schedule.Id}' cannot be deleted because it is used by one or more event types.");
    }

    [Fact]
    public async Task DeleteSchedule_WhenScheduleIsNotDefaultOrReferenced_ShouldRemoveSchedule()
    {
        await CreateScheduleAsync("Default schedule", true);
        var schedule = await CreateScheduleAsync("Temporary schedule", false);

        var response = await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/schedules/{schedule.Id}");
        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await AuthenticatedOwnerHttpClient.GetAsync($"/api/schedules/{schedule.Id}");
        await getResponse.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"Schedule '{schedule.Id}' was not found.");
    }

    private async Task<ScheduleResponse> CreateScheduleAsync(string name, bool isDefault)
    {
        var command = new
        {
            name,
            timeZone = "Africa/Johannesburg",
            isDefault,
            availabilityWindows = new[]
            {
                new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 }
            }
        };

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", command);
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<ScheduleResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record SchedulesResponse(ScheduleResponse[] Schedules);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id, string Name, string TimeZone, bool IsDefault, AvailabilityWindowResponse[] AvailabilityWindows);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record AvailabilityWindowResponse(int[] Days, int StartMinute, int EndMinute);
}

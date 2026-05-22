using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.EventTypes;

public sealed class HashedLinkEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task CreateHashedLink_WhenOwnerCreatesLink_ShouldPersistAndReturnLink()
    {
        var eventType = await CreateEventTypeAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/hashed-links",
            new { hash = "abc123def456", expiresAfterUses = 5 }
        );

        response.EnsureSuccessStatusCode();
        var created = await response.DeserializeResponse<HashedLinkTestResponse>();
        created.Should().NotBeNull();
        created!.Hash.Should().Be("abc123def456");
        created.ExpiresAfterUses.Should().Be(5);
        created.EventTypeId.Should().Be(eventType.Id);

        Connection.ExecuteScalar<long>("SELECT COUNT(1) FROM hashed_links WHERE id = @id", [new { id = created.Id }]).Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "HashedLinkCreated");
    }

    [Fact]
    public async Task CreateHashedLink_WhenHashIsOmitted_ShouldGenerateOne()
    {
        var eventType = await CreateEventTypeAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/hashed-links",
            new { }
        );

        response.EnsureSuccessStatusCode();
        var created = await response.DeserializeResponse<HashedLinkTestResponse>();
        created!.Hash.Should().NotBeNullOrEmpty();
        created.Hash.Length.Should().BeGreaterThan(8);
    }

    [Fact]
    public async Task CreateHashedLink_WhenHashAlreadyExists_ShouldReturnBadRequest()
    {
        var eventType = await CreateEventTypeAsync();
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/hashed-links",
            new { hash = "duplicate-hash" }
        );

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/hashed-links",
            new { hash = "duplicate-hash" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListHashedLinks_WhenLinksExist_ShouldReturnAll()
    {
        var eventType = await CreateEventTypeAsync();
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/event-types/{eventType.Id}/hashed-links", new { hash = "hash-1" });
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/event-types/{eventType.Id}/hashed-links", new { hash = "hash-2" });

        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{eventType.Id}/hashed-links");
        response.EnsureSuccessStatusCode();
        var listing = await response.DeserializeResponse<HashedLinksTestResponse>();

        listing!.HashedLinks.Should().HaveCount(2);
        listing.HashedLinks.Select(l => l.Hash).Should().BeEquivalentTo(["hash-1", "hash-2"]);
    }

    [Fact]
    public async Task DeleteHashedLink_WhenLinkExists_ShouldRemoveIt()
    {
        var eventType = await CreateEventTypeAsync();
        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/hashed-links",
            new { hash = "to-delete" }
        );
        var created = await createResponse.DeserializeResponse<HashedLinkTestResponse>();

        var deleteResponse = await AuthenticatedOwnerHttpClient.DeleteAsync(
            $"/api/event-types/{eventType.Id}/hashed-links/{created!.Id}"
        );

        deleteResponse.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>("SELECT COUNT(1) FROM hashed_links WHERE id = @id", [new { id = created.Id }]).Should().Be(0);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "HashedLinkDeleted");
    }

    [Fact]
    public async Task DeleteHashedLink_WhenLinkBelongsToAnotherEventType_ShouldReturnNotFound()
    {
        var eventType1 = await CreateEventTypeAsync("Intro one", "intro-one");
        var eventType2 = await CreateEventTypeAsync("Intro two", "intro-two");
        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType1.Id}/hashed-links",
            new { hash = "owned-by-1" }
        );
        var created = await createResponse.DeserializeResponse<HashedLinkTestResponse>();

        var deleteResponse = await AuthenticatedOwnerHttpClient.DeleteAsync(
            $"/api/event-types/{eventType2.Id}/hashed-links/{created!.Id}"
        );

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTeamAssignment_WhenOwnerUpdates_ShouldPersistAssignment()
    {
        var eventType = await CreateEventTypeAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/event-types/{eventType.Id}/team-assignment",
            new
            {
                assignAllTeamMembers = true,
                teamAssignment = new
                {
                    isRRWeightsEnabled = true,
                    maxLeadThreshold = 3
                }
            }
        );

        response.EnsureSuccessStatusCode();
        var updated = await response.DeserializeResponse<EventTypeTestResponse>();

        updated!.AssignAllTeamMembers.Should().BeTrue();
        updated.Settings.TeamAssignment.IsRRWeightsEnabled.Should().BeTrue();
        updated.Settings.TeamAssignment.MaxLeadThreshold.Should().Be(3);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "TeamAssignmentUpdated");
    }

    private async Task<EventTypeTestResponse> CreateEventTypeAsync(string title = "Intro call", string slug = "intro-call")
    {
        var scheduleCommand = new
        {
            name = "Working hours",
            timeZone = "Africa/Johannesburg",
            isDefault = true,
            availabilityWindows = new[]
            {
                new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 }
            }
        };
        var scheduleResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", scheduleCommand);
        scheduleResponse.EnsureSuccessStatusCode();
        var schedule = await scheduleResponse.DeserializeResponse<ScheduleTestResponse>();

        var eventTypeCommand = new
        {
            title,
            slug,
            description = "Description",
            durationMinutes = 30,
            hidden = false,
            scheduleId = schedule!.Id,
            beforeEventBufferMinutes = 0,
            afterEventBufferMinutes = 0,
            slotIntervalMinutes = 30,
            minimumBookingNoticeMinutes = 60,
            locationType = "link",
            locationValue = "https://example.com/meet"
        };
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", eventTypeCommand);
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeTestResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleTestResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record HashedLinkTestResponse(string Id, string EventTypeId, string Hash, int? ExpiresAfterUses, DateTimeOffset? ExpiresAt);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record HashedLinksTestResponse(HashedLinkTestResponse[] HashedLinks);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeTestResponse(string Id, bool AssignAllTeamMembers, EventTypeSettingsTestResponse Settings);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeSettingsTestResponse(EventTypeTeamAssignmentTestResponse TeamAssignment);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeTeamAssignmentTestResponse(bool IsRRWeightsEnabled, int? MaxLeadThreshold);
}

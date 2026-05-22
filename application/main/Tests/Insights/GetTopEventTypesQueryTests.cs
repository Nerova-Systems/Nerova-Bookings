using System.Net;
using FluentAssertions;
using Main.Features.Insights.Queries.GetTopEventTypes;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Insights;

public sealed class GetTopEventTypesQueryTests : InsightsEndpointBaseTest
{
    private const string Url = "/api/insights/top-event-types?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z";

    [Fact]
    public async Task GetTopEventTypes_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTopEventTypes_WhenNoBookings_ShouldReturnEmptyList()
    {
        var response = await InsightsClient.GetAsync(Url);
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<TopEventTypesResponse>();
        result!.EventTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopEventTypes_WhenMultipleEventTypes_ShouldReturnSortedByTotalBookings()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Intro Call", "intro-call");
        await CreateEventTypeAsync(schedule.Id, "Strategy Session", "strategy");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        // b3 uses a different day since all event types share the owner's availability
        var b1 = await CreateBookingAsync("intro-call", "2026-06-01T07:00:00Z");
        var b2 = await CreateBookingAsync("intro-call", "2026-06-01T09:00:00Z");
        var b3 = await CreateBookingAsync("strategy", "2026-06-02T07:00:00Z");
        Connection.Update("bookings", "id", b1.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z"))]);
        Connection.Update("bookings", "id", b2.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T09:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T09:30:00Z"))]);
        Connection.Update("bookings", "id", b3.Id, [("start_time", DateTimeOffset.Parse("2025-06-02T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-02T07:30:00Z"))]);

        // Act
        var response = await InsightsClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<TopEventTypesResponse>();
        result!.EventTypes.Should().HaveCount(2);
        result.EventTypes[0].Title.Should().Be("Intro Call");
        result.EventTypes[0].TotalCount.Should().Be(2);
        result.EventTypes[1].Title.Should().Be("Strategy Session");
        result.EventTypes[1].TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTopEventTypes_WhenBookingsCancelled_ShouldCalculateCancellationRate()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        var b1 = await CreateBookingAsync("consult", "2026-06-01T07:00:00Z");
        var b2 = await CreateBookingAsync("consult", "2026-06-01T09:00:00Z");
        Connection.Update("bookings", "id", b1.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z"))]);
        Connection.Update("bookings", "id", b2.Id, [
            ("start_time", DateTimeOffset.Parse("2025-06-01T09:00:00Z")),
            ("end_time", DateTimeOffset.Parse("2025-06-01T09:30:00Z")),
            ("status", "cancelled")
        ]);

        // Act
        var response = await InsightsClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<TopEventTypesResponse>();
        result!.EventTypes[0].CancelledCount.Should().Be(1);
        result.EventTypes[0].CancellationRate.Should().BeApproximately(0.5, 0.001);
    }
}

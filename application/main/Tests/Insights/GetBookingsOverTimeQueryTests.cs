using System.Net;
using FluentAssertions;
using Main.Features.Insights.Queries.GetBookingsOverTime;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Insights;

public sealed class GetBookingsOverTimeQueryTests : InsightsEndpointBaseTest
{
    [Fact]
    public async Task GetBookingsOverTime_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync("/api/insights/bookings-over-time?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookingsOverTime_WhenNoBookings_ShouldReturnEmptyDataPoints()
    {
        var response = await InsightsClient.GetAsync("/api/insights/bookings-over-time?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z");

        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<BookingsOverTimeResponse>();
        result!.DataPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBookingsOverTime_WhenGroupingByDay_ShouldReturnOneBucketPerDay()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        var b1 = await CreateBookingAsync("consult", "2026-06-01T07:00:00Z");
        var b2 = await CreateBookingAsync("consult", "2026-06-01T09:00:00Z");
        var b3 = await CreateBookingAsync("consult", "2026-06-02T07:00:00Z");
        Connection.Update("bookings", "id", b1.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z"))]);
        Connection.Update("bookings", "id", b2.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T09:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T09:30:00Z"))]);
        Connection.Update("bookings", "id", b3.Id, [("start_time", DateTimeOffset.Parse("2025-06-02T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-02T07:30:00Z"))]);

        // Act
        var response = await InsightsClient.GetAsync("/api/insights/bookings-over-time?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z&timeView=Day");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<BookingsOverTimeResponse>();
        result!.DataPoints.Should().HaveCount(2);
        result.DataPoints[0].Count.Should().Be(2); // June 1
        result.DataPoints[1].Count.Should().Be(1); // June 2
    }

    [Fact]
    public async Task GetBookingsOverTime_WhenGroupingByMonth_ShouldGroupCorrectly()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        var b1 = await CreateBookingAsync("consult", "2026-06-01T07:00:00Z");
        var b2 = await CreateBookingAsync("consult", "2026-06-02T07:00:00Z");
        Connection.Update("bookings", "id", b1.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z"))]);
        Connection.Update("bookings", "id", b2.Id, [("start_time", DateTimeOffset.Parse("2025-07-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-07-01T07:30:00Z"))]);

        // Act
        var response = await InsightsClient.GetAsync("/api/insights/bookings-over-time?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z&timeView=Month");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<BookingsOverTimeResponse>();
        result!.DataPoints.Should().HaveCount(2);
    }
}

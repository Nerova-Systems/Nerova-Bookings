using System.Net;
using FluentAssertions;
using Main.Features.Insights.Queries.GetBookingHeatmap;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Insights;

public sealed class GetBookingHeatmapQueryTests : InsightsEndpointBaseTest
{
    private const string Url = "/api/insights/heatmap?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z";

    [Fact]
    public async Task GetBookingHeatmap_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookingHeatmap_WhenNoBookings_ShouldReturnEmptyGrid()
    {
        var response = await InsightsClient.GetAsync(Url);
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<BookingHeatmapResponse>();
        result!.Cells.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBookingHeatmap_WhenBookingsExist_ShouldReturnCellsWithCorrectDayAndHour()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at a future weekday slot; Connection.Update sets the desired Sunday date
        var b1 = await CreateBookingAsync("consult", "2026-06-01T07:00:00Z");
        Connection.Update("bookings", "id", b1.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z"))]);

        // Act — UTC timezone so local hour == UTC hour
        var response = await InsightsClient.GetAsync(Url + "&timeZone=UTC");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<BookingHeatmapResponse>();
        result!.Cells.Should().HaveCount(1);
        result.Cells[0].DayOfWeek.Should().Be((int)DayOfWeek.Sunday); // 0
        result.Cells[0].Hour.Should().Be(7);
        result.Cells[0].Count.Should().Be(1);
    }

    [Fact]
    public async Task GetBookingHeatmap_WhenInvalidTimezone_ShouldReturnValidationError()
    {
        var response = await InsightsClient.GetAsync(Url + "&timeZone=NotARealTimezone");
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, (string?)null);
    }
}

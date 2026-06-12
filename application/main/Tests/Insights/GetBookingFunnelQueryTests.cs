using System.Net;
using FluentAssertions;
using Main.Features.Insights.Queries.GetBookingFunnel;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using static Main.Tests.DateDriftTestDates;

namespace Main.Tests.Insights;

public sealed class GetBookingFunnelQueryTests : InsightsEndpointBaseTest
{
    private const string Url = "/api/insights/funnel?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z";

    [Fact]
    public async Task GetBookingFunnel_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookingFunnel_WhenNoBookings_ShouldReturnAllZeros()
    {
        var response = await InsightsClient.GetAsync(Url);
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<BookingFunnelResponse>();
        result!.Created.Should().Be(0);
        result.Accepted.Should().Be(0);
        result.Completed.Should().Be(0);
    }

    [Fact]
    public async Task GetBookingFunnel_WhenBookingsInVariousStates_ShouldReturnCorrectFunnelCounts()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        var completed = await CreateBookingAsync("consult", FutureDateTimeText(0, 7, 0));
        // accepted + future → accepted only
        var accepted = await CreateBookingAsync("consult", FutureDateTimeText(0, 9, 0));
        // cancelled → created but not accepted
        var cancelled = await CreateBookingAsync("consult", FutureDateTimeText(0, 11, 0));

        Connection.Update("bookings", "id", completed.Id, [
                ("start_time", DateTimeOffset.Parse("2025-06-02T07:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-02T07:30:00Z"))
            ]
        );
        // end_time set to far future so it is NOT yet completed
        Connection.Update("bookings", "id", accepted.Id, [
                ("start_time", DateTimeOffset.Parse("2025-06-02T09:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2027-06-02T09:30:00Z"))
            ]
        );
        Connection.Update("bookings", "id", cancelled.Id, [
                ("start_time", DateTimeOffset.Parse("2025-06-02T11:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-02T11:30:00Z")),
                ("status", "Cancelled")
            ]
        );

        // Act
        var response = await InsightsClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<BookingFunnelResponse>();
        result!.Created.Should().Be(3);
        result.Accepted.Should().Be(2); // completed + accepted (both status=="accepted")
        result.Completed.Should().Be(1); // completed has end_time in past
    }
}

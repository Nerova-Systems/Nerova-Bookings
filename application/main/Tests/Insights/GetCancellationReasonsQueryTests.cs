using System.Net;
using FluentAssertions;
using Main.Features.Insights.Queries.GetCancellationReasons;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using static Main.Tests.DateDriftTestDates;

namespace Main.Tests.Insights;

public sealed class GetCancellationReasonsQueryTests : InsightsEndpointBaseTest
{
    private const string Url = "/api/insights/cancellation-reasons?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z";

    [Fact]
    public async Task GetCancellationReasons_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCancellationReasons_WhenNoCancellations_ShouldReturnEmptyList()
    {
        var response = await InsightsClient.GetAsync(Url);
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<CancellationReasonsResponse>();
        result!.Reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCancellationReasons_WhenCancelledBookingsHaveNoReason_ShouldGroupAsNoReasonProvided()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at a future weekday slot; Connection.Update moves it to the analytics date range
        var b = await CreateBookingAsync("consult", FutureDateTimeText(0, 7, 0));
        Connection.Update("bookings", "id", b.Id, [
                ("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z")),
                ("status", "Cancelled")
            ]
        );

        // Act
        var response = await InsightsClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<CancellationReasonsResponse>();
        result!.Reasons.Should().HaveCount(1);
        result.Reasons[0].Reason.Should().Be("No reason provided");
        result.Reasons[0].Count.Should().Be(1);
    }

    [Fact]
    public async Task GetCancellationReasons_WhenBookingsHaveReason_ShouldExtractAndGroupByReason()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        var b1 = await CreateBookingAsync("consult", FutureDateTimeText(0, 7, 0));
        var b2 = await CreateBookingAsync("consult", FutureDateTimeText(0, 9, 0));
        Connection.Update("bookings", "id", b1.Id, [
                ("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z")),
                ("status", "Cancelled"),
                ("responses_json", """{"cancellationReason":"Schedule conflict"}""")
            ]
        );
        Connection.Update("bookings", "id", b2.Id, [
                ("start_time", DateTimeOffset.Parse("2025-06-01T09:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-01T09:30:00Z")),
                ("status", "Cancelled"),
                ("responses_json", """{"cancellationReason":"Schedule conflict"}""")
            ]
        );

        // Act
        var response = await InsightsClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<CancellationReasonsResponse>();
        result!.Reasons.Should().HaveCount(1);
        result.Reasons[0].Reason.Should().Be("Schedule conflict");
        result.Reasons[0].Count.Should().Be(2);
    }
}

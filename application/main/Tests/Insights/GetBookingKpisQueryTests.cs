using System.Net;
using FluentAssertions;
using Main.Features.Insights.Queries.GetBookingKpis;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using static Main.Tests.DateDriftTestDates;

namespace Main.Tests.Insights;

public sealed class GetBookingKpisQueryTests : InsightsEndpointBaseTest
{
    private const string Url = "/api/insights/kpis?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z";

    [Fact]
    public async Task GetBookingKpis_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBookingKpis_WhenFeatureFlagDisabled_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBookingKpis_WhenInvalidDateRange_ShouldReturnValidationError()
    {
        var response = await InsightsClient.GetAsync("/api/insights/kpis?from=2026-01-01T00:00:00Z&to=2025-01-01T00:00:00Z");
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, (string?)null);
    }

    [Fact]
    public async Task GetBookingKpis_WhenNoBookingsInRange_ShouldReturnAllZeros()
    {
        var response = await InsightsClient.GetAsync(Url);

        response.ShouldBeSuccessfulGetRequest();
        var kpis = await response.DeserializeResponse<BookingKpisResponse>();
        kpis!.TotalCount.Should().Be(0);
        kpis.AcceptedCount.Should().Be(0);
        kpis.PendingCount.Should().Be(0);
        kpis.CancelledCount.Should().Be(0);
        kpis.CompletedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetBookingKpis_WhenOwnerHasBookings_ShouldReturnCorrectCounts()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consultation", "consultation");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        var accepted = await CreateBookingAsync("consultation", FutureDateTimeText(0, 7, 0));
        var cancelled = await CreateBookingAsync("consultation", FutureDateTimeText(0, 9, 0));
        var pending = await CreateBookingAsync("consultation", FutureDateTimeText(0, 11, 0));
        Connection.Update("bookings", "id", accepted.Id, [
                ("start_time", DateTimeOffset.Parse("2025-06-02T07:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-02T07:30:00Z"))
            ]
        );
        Connection.Update("bookings", "id", cancelled.Id, [
                ("status", "Cancelled"),
                ("start_time", DateTimeOffset.Parse("2025-06-02T09:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-02T09:30:00Z"))
            ]
        );
        Connection.Update("bookings", "id", pending.Id, [
                ("status", "Pending"),
                ("start_time", DateTimeOffset.Parse("2025-06-02T11:00:00Z")),
                ("end_time", DateTimeOffset.Parse("2025-06-02T11:30:00Z"))
            ]
        );

        // Act
        var response = await InsightsClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var kpis = await response.DeserializeResponse<BookingKpisResponse>();
        kpis!.TotalCount.Should().Be(3);
        kpis.AcceptedCount.Should().Be(1);
        kpis.PendingCount.Should().Be(1);
        kpis.CancelledCount.Should().Be(1);
        kpis.CompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetBookingKpis_WhenBookingBelongsToOtherTenant_ShouldNotBeIncluded()
    {
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consultation", "consultation");
        var booking = await CreateBookingAsync("consultation", FutureDateTimeText(0, 7, 0));
        Connection.Update("bookings", "id", booking.Id, [("tenant_id", 99999L)]);

        var response = await InsightsClient.GetAsync(Url);

        response.ShouldBeSuccessfulGetRequest();
        var kpis = await response.DeserializeResponse<BookingKpisResponse>();
        kpis!.TotalCount.Should().Be(0);
    }
}

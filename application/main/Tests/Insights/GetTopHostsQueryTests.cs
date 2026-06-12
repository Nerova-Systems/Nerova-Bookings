using System.Net;
using FluentAssertions;
using Main.Features.Insights.Queries.GetTopHosts;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;
using static Main.Tests.DateDriftTestDates;

namespace Main.Tests.Insights;

public sealed class GetTopHostsQueryTests : InsightsEndpointBaseTest
{
    private const string Url = "/api/insights/top-hosts?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z";

    [Fact]
    public async Task GetTopHosts_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.GetAsync(Url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTopHosts_WhenNoBookings_ShouldReturnEmptyList()
    {
        var response = await InsightsClient.GetAsync(Url);
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<TopHostsResponse>();
        result!.Hosts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTopHosts_WhenOwnerHasBookings_ShouldReturnHostEntry()
    {
        // Arrange
        await UpdateSchedulingProfileAsync("owner");
        var schedule = await CreateScheduleAsync();
        await CreateEventTypeAsync(schedule.Id, "Consult", "consult");
        // Create at future weekday slots; Connection.Update moves them to the analytics date range
        var b1 = await CreateBookingAsync("consult", FutureDateTimeText(0, 7, 0));
        var b2 = await CreateBookingAsync("consult", FutureDateTimeText(0, 9, 0));
        Connection.Update("bookings", "id", b1.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T07:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T07:30:00Z"))]);
        Connection.Update("bookings", "id", b2.Id, [("start_time", DateTimeOffset.Parse("2025-06-01T09:00:00Z")), ("end_time", DateTimeOffset.Parse("2025-06-01T09:30:00Z"))]);

        // Act
        var response = await InsightsClient.GetAsync(Url);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<TopHostsResponse>();
        result!.Hosts.Should().HaveCount(1);
        result.Hosts[0].TotalCount.Should().Be(2);
        result.Hosts[0].HostUserId.Value.Should().Be(DatabaseSeeder.Tenant1Owner.Id!.Value);
    }
}

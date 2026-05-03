using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class ScheduleDowngradeTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ScheduleDowngrade_WhenPaidPlanDowngradeRequested_ShouldReturnNotFound()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("paystack_customer_id", "CUS_test_123"),
                ("paystack_subscription_id", "SUB_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new { TargetPlan = SubscriptionPlan.Standard };
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/schedule-downgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }
}

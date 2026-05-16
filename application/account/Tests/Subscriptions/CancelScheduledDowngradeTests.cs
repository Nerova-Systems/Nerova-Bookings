using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class CancelScheduledDowngradeTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task CancelScheduledDowngrade_WhenDowngradeScheduled_ShouldClearScheduledPlan()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("scheduled_plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "AUTH_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel-downgrade", new { });

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<string?>("SELECT scheduled_plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().BeNull();
        TelemetryEventsCollectorSpy.CollectedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelScheduledDowngrade_WhenNoDowngradeScheduled_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "AUTH_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel-downgrade", new { });

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No scheduled downgrade to cancel.");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }
}

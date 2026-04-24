using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class CancelScheduledDowngradeTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task CancelScheduledDowngrade_WhenScheduledDowngradeExists_ShouldClearScheduledPlan()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("scheduled_plan", nameof(SubscriptionPlan.Standard)),
                ("current_period_end", TimeProvider.System.GetUtcNow().AddDays(10))
            ]
        );
        var command = new CancelScheduledDowngradeCommand();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel-scheduled-downgrade", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var scheduledPlan = Connection.ExecuteScalar<string?>("SELECT scheduled_plan FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        scheduledPlan.Should().BeNull();
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionDowngradeCancelled");
    }

    [Fact]
    public async Task CancelScheduledDowngrade_WhenNoScheduledDowngrade_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Premium))
            ]
        );
        var command = new CancelScheduledDowngradeCommand();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel-scheduled-downgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No scheduled downgrade to cancel.");
    }

    [Fact]
    public async Task CancelScheduledDowngrade_WhenNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new CancelScheduledDowngradeCommand();

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel-scheduled-downgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

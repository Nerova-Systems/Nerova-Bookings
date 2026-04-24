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

public sealed class ScheduleDowngradeTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ScheduleDowngrade_WhenActivePremiumAndOwner_ShouldScheduleDowngradeToStandard()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("current_period_end", TimeProvider.System.GetUtcNow().AddDays(15))
            ]
        );
        var command = new ScheduleDowngradeCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/schedule-downgrade", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var scheduledPlan = Connection.ExecuteScalar<string?>("SELECT scheduled_plan FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        scheduledPlan.Should().Be(nameof(SubscriptionPlan.Standard));
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionDowngradeScheduled");
    }

    [Fact]
    public async Task ScheduleDowngrade_WhenActiveStandardAndOwner_ShouldScheduleDowngradeToStarter()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );
        var command = new ScheduleDowngradeCommand(SubscriptionPlan.Starter);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/schedule-downgrade", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
    }

    [Fact]
    public async Task ScheduleDowngrade_WhenNotActive_ShouldReturnBadRequest()
    {
        // Arrange — default is Trial
        var command = new ScheduleDowngradeCommand(SubscriptionPlan.Starter);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/schedule-downgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription must be active to schedule a downgrade.");
    }

    [Fact]
    public async Task ScheduleDowngrade_WhenUpgradePlanRequested_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Starter))
            ]
        );
        var command = new ScheduleDowngradeCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/schedule-downgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScheduleDowngrade_WhenTrialPlanRequested_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );
        var command = new ScheduleDowngradeCommand(SubscriptionPlan.Trial);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/schedule-downgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScheduleDowngrade_WhenNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new ScheduleDowngradeCommand(SubscriptionPlan.Starter);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/schedule-downgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

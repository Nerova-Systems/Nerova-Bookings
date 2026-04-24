using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class UpgradeSubscriptionTests : EndpointBaseTest<AccountDbContext>
{
    private const string TestToken = "test-payfast-token-upgrade";

    [Fact]
    public async Task UpgradeSubscription_WhenActiveStarterAndOwner_ShouldUpgradeToStandard()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Starter)),
                ("pay_fast_token", TestToken),
                ("current_period_start", TimeProvider.System.GetUtcNow().AddDays(-15)),
                ("current_period_end", TimeProvider.System.GetUtcNow().AddDays(15))
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var plan = Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        plan.Should().Be(nameof(SubscriptionPlan.Standard));
        await PayFastClient.Received(1).ChargeTokenAsync(TestToken, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionUpgraded");
    }

    [Fact]
    public async Task UpgradeSubscription_WhenActiveStandardAndOwner_ShouldUpgradeToPremium()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken),
                ("current_period_start", TimeProvider.System.GetUtcNow().AddDays(-10)),
                ("current_period_end", TimeProvider.System.GetUtcNow().AddDays(20))
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Premium);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var plan = Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        plan.Should().Be(nameof(SubscriptionPlan.Premium));
    }

    [Fact]
    public async Task UpgradeSubscription_WhenNotActive_ShouldReturnBadRequest()
    {
        // Arrange — subscription is in Trial by default
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription must be active to upgrade.");
    }

    [Fact]
    public async Task UpgradeSubscription_WhenDowngradePlanRequested_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("pay_fast_token", TestToken)
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpgradeSubscription_WhenSamePlanRequested_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken)
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpgradeSubscription_WhenNoToken_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Starter))
            ]
        );
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No payment method on file. Please contact support.");
    }

    [Fact]
    public async Task UpgradeSubscription_WhenPaymentFails_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Starter)),
                ("pay_fast_token", TestToken),
                ("current_period_start", TimeProvider.System.GetUtcNow().AddDays(-1)),
                ("current_period_end", TimeProvider.System.GetUtcNow().AddDays(29))
            ]
        );
        PayFastClient.ChargeTokenAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Payment failed. Please check your payment method and try again.");
    }

    [Fact]
    public async Task UpgradeSubscription_WhenNotOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new UpgradeSubscriptionCommand(SubscriptionPlan.Standard);

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/upgrade", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

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

public sealed class RetryFailedChargeTests : EndpointBaseTest<AccountDbContext>
{
    private const string TestToken = "test-payfast-token-retry";

    [Fact]
    public async Task RetryFailedCharge_WhenPastDueWithTokenAndOwner_ShouldSucceedAndActivate()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.PastDue)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken),
                ("first_payment_failed_at", TimeProvider.System.GetUtcNow().AddDays(-3))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/retry-charge", new RetryFailedChargeCommand());

        // Assert
        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        status.Should().Be(nameof(SubscriptionStatus.Active));
        await PayFastClient.Received(1).ChargeTokenAsync(TestToken, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("PaymentRecovered");
    }

    [Fact]
    public async Task RetryFailedCharge_WhenNotPastDue_ShouldReturnBadRequest()
    {
        // Arrange — subscription is in Trial by default
        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/retry-charge", new RetryFailedChargeCommand());

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription is not past due. Nothing to retry.");
    }

    [Fact]
    public async Task RetryFailedCharge_WhenPastDueWithNoToken_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.PastDue)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/retry-charge", new RetryFailedChargeCommand());

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No payment method on file. Please reactivate with a new payment method.");
    }

    [Fact]
    public async Task RetryFailedCharge_WhenPaymentFails_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.PastDue)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken)
            ]
        );
        PayFastClient.ChargeTokenAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/retry-charge", new RetryFailedChargeCommand());

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Payment retry failed. Please update your payment method and try again.");
    }

    [Fact]
    public async Task RetryFailedCharge_WhenNotOwner_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/retry-charge", new RetryFailedChargeCommand());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

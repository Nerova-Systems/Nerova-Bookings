using System.Globalization;
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

public sealed class CancelSubscriptionTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task CancelSubscription_WhenActiveSubscription_ShouldSucceed()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_price_amount", 29.00m),
                ("current_price_currency", "ZAR"),
                ("current_period_start", TimeProvider.GetUtcNow().AddDays(-15)),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new CancelSubscriptionCommand(CancellationReason.TooExpensive, "The price doubled last month.");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        response.EnsureSuccessStatusCode();

        Connection.ExecuteScalar<long>("SELECT cancel_at_period_end FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT cancellation_reason FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(CancellationReason.TooExpensive));
        Connection.ExecuteScalar<string>("SELECT cancellation_feedback FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be("The price doubled last month.");
        Connection.ExecuteScalar<string>("SELECT event_type FROM billing_events WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(BillingEventType.SubscriptionCancelled));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT amount_delta FROM billing_events WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(-29.00m);
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT committed_mrr FROM billing_events WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(0m);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "SubscriptionCancelled");
    }

    [Fact]
    public async Task CancelSubscription_WhenBasisSubscription_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new CancelSubscriptionCommand(CancellationReason.NoLongerNeeded, null);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Cannot cancel a Basis subscription.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task CancelSubscription_WhenAlreadyCancelled_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30)),
                ("cancel_at_period_end", true)
            ]
        );
        var command = new CancelSubscriptionCommand(CancellationReason.FoundAlternative, "Switched to competitor.");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Subscription is already scheduled for cancellation.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task CancelSubscription_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new CancelSubscriptionCommand(CancellationReason.Other, "Just testing.");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/cancel", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }
}

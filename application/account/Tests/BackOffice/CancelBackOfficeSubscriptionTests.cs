using System.Globalization;
using System.Net;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Authentication.MockEasyAuth;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.BackOffice;

public sealed class CancelBackOfficeSubscriptionTests : BackOfficeEndpointBaseTest
{
    [Fact]
    public async Task CancelBackOfficeSubscription_WhenActiveSubscription_ShouldSucceed()
    {
        // Arrange
        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [
                ("state", nameof(TenantState.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_backoffice_cancel"),
                ("paystack_authorization_code", "auth_test_backoffice_cancel"),
                ("current_price_amount", 29.00m),
                ("current_price_currency", "ZAR"),
                ("current_period_start", DateTimeOffset.UtcNow.AddDays(-15)),
                ("current_period_end", DateTimeOffset.UtcNow.AddDays(30))
            ]
        );

        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/cancel-subscription", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Connection.ExecuteScalar<long>("SELECT cancel_at_period_end FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT cancellation_reason FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(CancellationReason.CancelledByAdmin));
        Connection.ExecuteScalar<string?>("SELECT cancellation_feedback FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().BeNull();
        Connection.ExecuteScalar<string>("SELECT event_type FROM billing_events WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(BillingEventType.SubscriptionCancelled));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT amount_delta FROM billing_events WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(-29.00m);
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT committed_mrr FROM billing_events WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(0m);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "SubscriptionCancelled");
    }

    [Fact]
    public async Task CancelBackOfficeSubscription_WhenCalledByNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/cancel-subscription", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

using System.Net;
using System.Text;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class AcknowledgePaystackWebhookTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string WebhookUrl = "/api/account/subscriptions/paystack-webhook";

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenInvalidSignature_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode)
            ]
        );
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent("""{"event":"charge.success","data":{"customer":{"customer_code":"CUS_mock_12345"}}}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "invalid_signature");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Invalid webhook signature.");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenDuplicateEvent_ShouldReturnSuccess()
    {
        // Arrange
        var eventId = $"{MockPaystackClient.MockWebhookEventId}_duplicate";
        Connection.Insert("paystack_events", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", eventId),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("event_type", "charge.success"),
                ("status", nameof(PaystackEventStatus.Processed)),
                ("processed_at", TimeProvider.GetUtcNow()),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_reference", MockPaystackClient.MockReference),
                ("payload", null),
                ("error", null)
            ]
        );

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerCode}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", $"event_type:charge.success,event_id:{eventId}");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenNoSubscriptionFound_ShouldStorePendingEvent()
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerCode}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:charge.success");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var eventCount = Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM paystack_events WHERE paystack_customer_code = @customerCode", [new { customerCode = MockPaystackClient.MockCustomerCode }]);
        eventCount.Should().Be(1);

        var eventStatus = Connection.ExecuteScalar<string>("SELECT status FROM paystack_events WHERE paystack_customer_code = @customerCode", [new { customerCode = MockPaystackClient.MockCustomerCode }]);
        eventStatus.Should().Be(nameof(PaystackEventStatus.Pending));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenPendingSubscribeAttemptPaid_ShouldActivateSubscriptionAndProcessEvent()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode)
            ]
        );
        var subscriptionId = Connection.ExecuteScalar<string>("SELECT id FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        Connection.Insert("paystack_payment_attempts", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", PaystackPaymentAttemptId.NewId().ToString()),
                ("subscription_id", subscriptionId),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("paystack_reference", MockPaystackClient.MockReference),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", null),
                ("purpose", nameof(PaystackPaymentPurpose.Subscribe)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("amount", 29.00m),
                ("currency", MockPaystackClient.MockStandardCurrency),
                ("status", nameof(PaystackPaymentAttemptStatus.Pending)),
                ("completed_at", null),
                ("failure_reason", null)
            ]
        );

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerCode}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:charge.success");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_events WHERE paystack_reference = @reference", [new { reference = MockPaystackClient.MockReference }]).Should().Be(nameof(PaystackEventStatus.Processed));
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE paystack_reference = @reference", [new { reference = MockPaystackClient.MockReference }]).Should().Be(nameof(PaystackPaymentAttemptStatus.Succeeded));
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string>("SELECT paystack_authorization_code FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(MockPaystackClient.MockAuthorizationCode);
        Connection.ExecuteScalar<string>("SELECT plan FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        var transactions = Connection.ExecuteScalar<string>("SELECT payment_transactions FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        transactions.Should().Contain("\"Amount\":29");
        transactions.Should().Contain("\"Status\":\"Succeeded\"");
    }
}

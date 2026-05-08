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

public sealed class AcknowledgePaystackWebhookTests : EndpointBaseTest<AccountDbContext>
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
}

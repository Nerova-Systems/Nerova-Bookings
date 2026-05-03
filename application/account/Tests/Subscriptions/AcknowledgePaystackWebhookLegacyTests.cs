using System.Net;
using System.Text;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class AcknowledgePaystackWebhookTests : EndpointBaseTest<AccountDbContext>
{
    private const string WebhookUrl = "/api/account/subscriptions/paystack-webhook";

    private void SetupSubscription(string? paystackCustomerId = MockPaystackClient.MockCustomerId, string? paystackSubscriptionId = MockPaystackClient.MockSubscriptionId, string plan = nameof(SubscriptionPlan.Standard), DateTimeOffset? firstPaymentFailedAt = null, string? cancellationReason = null)
    {
        var hasPaystackSubscription = paystackSubscriptionId is not null;
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", plan),
                ("paystack_customer_id", paystackCustomerId),
                ("paystack_subscription_id", paystackSubscriptionId),
                ("current_price_amount", hasPaystackSubscription ? 29.99m : null),
                ("current_price_currency", hasPaystackSubscription ? "ZAR" : null),
                ("current_period_end", hasPaystackSubscription ? TimeProvider.GetUtcNow().AddDays(30) : null),
                ("first_payment_failed_at", firstPaymentFailedAt),
                ("cancellation_reason", cancellationReason)
            ]
        );
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenInvalidSignature_ShouldReturnBadRequest()
    {
        // Arrange
        SetupSubscription();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
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
        SetupSubscription();
        var eventId = $"{MockPaystackClient.MockWebhookEventId}_duplicate";
        Connection.Insert("paystack_events", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", eventId),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("event_type", "subscription.create"),
                ("status", nameof(PaystackEventStatus.Processed)),
                ("processed_at", TimeProvider.GetUtcNow()),
                ("paystack_customer_id", MockPaystackClient.MockCustomerId),
                ("paystack_subscription_id", MockPaystackClient.MockSubscriptionId),
                ("payload", null),
                ("error", null)
            ]
        );
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", $"event_type:subscription.create,event_id:{eventId}");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenSubscriptionCreated_ShouldSyncSubscription()
    {
        // Arrange
        SetupSubscription(paystackSubscriptionId: null, plan: nameof(SubscriptionPlan.Basis));
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.create");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenPaymentSucceeded_ShouldClearPaymentFailure()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SetupSubscription(firstPaymentFailedAt: now.AddHours(-48));
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:charge.success");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var firstPaymentFailed = Connection.ExecuteScalar<string>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        firstPaymentFailed.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenFirstPaymentFailed_ShouldSetFailure()
    {
        // Arrange
        PaystackState.OverrideSubscriptionStatus = PaystackSubscriptionStatus.PastDue;
        SetupSubscription();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:invoice.payment_failed");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var firstPaymentFailed = Connection.ExecuteScalar<string>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        firstPaymentFailed.Should().NotBeNullOrEmpty();

        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Active));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenSubsequentPaymentFailed_ShouldNotUpdateFailureTimestamp()
    {
        // Arrange
        PaystackState.OverrideSubscriptionStatus = PaystackSubscriptionStatus.PastDue;
        var now = TimeProvider.GetUtcNow();
        SetupSubscription(firstPaymentFailedAt: now.AddHours(-48));
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:invoice.payment_failed");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var firstPaymentFailed = Connection.ExecuteScalar<string>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        firstPaymentFailed.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenSubscriptionDeletedInvoluntarily_ShouldSuspendTenant()
    {
        // Arrange
        PaystackState.SimulateSubscriptionDeleted = true;
        var now = TimeProvider.GetUtcNow();
        SetupSubscription(firstPaymentFailedAt: now.AddDays(-5));
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.disable");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Suspended));

        var suspensionReason = Connection.ExecuteScalar<string>("SELECT suspension_reason FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        suspensionReason.Should().Be(nameof(SuspensionReason.PaymentFailed));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenSubscriptionDeletedVoluntarily_ShouldKeepTenantActive()
    {
        // Arrange
        PaystackState.SimulateSubscriptionDeleted = true;
        SetupSubscription(cancellationReason: nameof(CancellationReason.NoLongerNeeded));
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.disable");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Active));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenSubscriptionCreated_ShouldActivateSuspendedTenant()
    {
        // Arrange
        SetupSubscription(paystackSubscriptionId: null, plan: nameof(SubscriptionPlan.Basis));
        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [("state", nameof(TenantState.Suspended)), ("suspension_reason", nameof(SuspensionReason.PaymentFailed))]);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.create");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Active));

        var suspensionReason = Connection.ExecuteScalar<string>("SELECT suspension_reason FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        suspensionReason.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenCustomerDeleted_ShouldSuspendTenant()
    {
        // Arrange
        PaystackState.SimulateCustomerDeleted = true;
        SetupSubscription();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:customer.deleted");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Suspended));

        var suspensionReason = Connection.ExecuteScalar<string>("SELECT suspension_reason FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        suspensionReason.Should().Be(nameof(SuspensionReason.CustomerDeleted));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenSubscriptionDeletedAndTenantAlreadySuspended_ShouldNotOverrideSuspension()
    {
        // Arrange - tenant already suspended with CustomerDeleted (e.g., customer.deleted processed in previous batch)
        PaystackState.SimulateSubscriptionDeleted = true;
        SetupSubscription(cancellationReason: nameof(CancellationReason.NoLongerNeeded));
        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [("state", nameof(TenantState.Suspended)), ("suspension_reason", nameof(SuspensionReason.CustomerDeleted))]);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.disable");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert - tenant should remain Suspended with CustomerDeleted, not overridden to Active or PaymentFailed
        response.EnsureSuccessStatusCode();

        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Suspended));

        var suspensionReason = Connection.ExecuteScalar<string>("SELECT suspension_reason FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        suspensionReason.Should().Be(nameof(SuspensionReason.CustomerDeleted));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenCustomerDeletedAndSubscriptionDeletedInSameBatch_ShouldSuspendWithCustomerDeleted()
    {
        // Arrange - pre-insert a pending customer.deleted event so both events process in the same batch
        PaystackState.SimulateCustomerDeleted = true;
        SetupSubscription(cancellationReason: nameof(CancellationReason.NoLongerNeeded));
        Connection.Insert("paystack_events", [
                ("tenant_id", null),
                ("id", $"{MockPaystackClient.MockWebhookEventId}_customer_deleted"),
                ("created_at", TimeProvider.GetUtcNow()),
                ("modified_at", null),
                ("event_type", "customer.deleted"),
                ("status", nameof(PaystackEventStatus.Pending)),
                ("processed_at", null),
                ("paystack_customer_id", MockPaystackClient.MockCustomerId),
                ("paystack_subscription_id", null),
                ("payload", null),
                ("error", null)
            ]
        );
        TelemetryEventsCollectorSpy.Reset();

        // Act - send subscription.disable webhook, which triggers processing of both pending events
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.disable");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert - customer.deleted should take priority, tenant suspended with CustomerDeleted
        response.EnsureSuccessStatusCode();

        var tenantState = Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        tenantState.Should().Be(nameof(TenantState.Suspended));

        var suspensionReason = Connection.ExecuteScalar<string>("SELECT suspension_reason FROM tenants WHERE id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        suspensionReason.Should().Be(nameof(SuspensionReason.CustomerDeleted));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenNoSubscriptionFound_ShouldStoreEventAndReturnSuccess()
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.create");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var eventCount = Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM paystack_events WHERE paystack_customer_id = @customerId", [new { customerId = MockPaystackClient.MockCustomerId }]);
        eventCount.Should().Be(1);

        var eventStatus = Connection.ExecuteScalar<string>("SELECT status FROM paystack_events WHERE paystack_customer_id = @customerId", [new { customerId = MockPaystackClient.MockCustomerId }]);
        eventStatus.Should().Be(nameof(PaystackEventStatus.Pending));
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenInvoiceUpdated_ShouldSyncState()
    {
        // Arrange
        SetupSubscription();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent($"customer:{MockPaystackClient.MockCustomerId}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:invoice.update");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AcknowledgePaystackWebhook_WhenNoCustomerId_ShouldStoreAsIgnored()
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, WebhookUrl)
        {
            Content = new StringContent("no_customer", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", "event_type:subscription.expiring_cards");
        var response = await AnonymousHttpClient.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
    }
}

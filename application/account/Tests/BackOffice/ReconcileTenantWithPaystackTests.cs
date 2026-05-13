using System.Net;
using System.Net.Http.Json;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.OAuth;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Authentication.MockEasyAuth;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.BackOffice;

public sealed class ReconcileTenantWithPaystackTests : BackOfficeEndpointBaseTest
{
    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenPendingSubscribeAttemptHasNoWebhook_ShouldActivateSubscriptionAndAppendBillingEvent()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("billing_info", """{"Name":"Test Organization","Address":null,"Email":"billing@example.com","TaxId":null}""")
            ]
        );
        var subscriptionId = Connection.ExecuteScalar<string>("SELECT id FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        var reference = $"nerova_reconcile_{Guid.NewGuid():N}";
        Connection.Insert("paystack_payment_attempts", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", PaystackPaymentAttemptId.NewId().ToString()),
                ("subscription_id", subscriptionId),
                ("created_at", DateTimeOffset.UtcNow.AddMinutes(-15)),
                ("modified_at", null),
                ("paystack_reference", reference),
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

        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        client.DefaultRequestHeaders.Add("Cookie", $"{OAuthProviderFactory.UseMockProviderCookieName}=true");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-paystack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReconcileTenantWithPaystackResponse>();
        payload.Should().NotBeNull();
        payload!.BillingEventsAppended.Should().Be(1);
        payload.RecoveredPaymentAttempts.Should().Be(1);
        payload.ArchivedEventsAwaitingConfirmation.Should().BeNull();
        payload.ReconciledAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));

        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE paystack_reference = @reference", [new { reference }]).Should().Be(nameof(PaystackPaymentAttemptStatus.Succeeded));
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string>("SELECT plan FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));

        var billingEvent = Connection.ExecuteScalar<string>("SELECT event_type FROM billing_events WHERE stripe_event_id = @stripeEventId", [new { stripeEventId = $"paystack:{reference}:Subscribe" }]);
        billingEvent.Should().Be(nameof(BillingEventType.SubscriptionCreated));
        var subscribedSince = Connection.ExecuteScalar<string>("SELECT subscribed_since FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        subscribedSince.Should().NotBeNullOrWhiteSpace();
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "SubscriptionCreated");
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenCalledThroughStripeCompatibilityRoute_ShouldUsePaystackReconciliation()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode)
            ]
        );

        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        client.DefaultRequestHeaders.Add("Cookie", $"{OAuthProviderFactory.UseMockProviderCookieName}=true");

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-stripe", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReconcileTenantWithPaystackResponse>();
        payload.Should().NotBeNull();
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenSubscriptionHasNoPaystackCustomer_ShouldReturnBadRequest()
    {
        // Arrange
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-paystack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenCalledByNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-paystack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenCalledWithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        using var client = CreateBackOfficeClient();

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-paystack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public sealed record ReconcileTenantWithPaystackResponse(
    int BillingEventsAppended,
    int RecoveredPaymentAttempts,
    bool HasDriftDetected,
    int DriftDiscrepancyCount,
    DateTimeOffset ReconciledAt,
    ArchivedEventsAwaitingConfirmation? ArchivedEventsAwaitingConfirmation
);

public sealed record ArchivedEventsAwaitingConfirmation(int Count, DateTimeOffset OldestOccurredAt, DateTimeOffset NewestOccurredAt);

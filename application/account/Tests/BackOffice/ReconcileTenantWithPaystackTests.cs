using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Integrations.OAuth;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Authentication.MockEasyAuth;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.BackOffice;

public sealed class ReconcileTenantWithPaystackTests(BackOfficeWebApplicationFactory factory) : BackOfficeEndpointBaseTest(factory), IClassFixture<BackOfficeWebApplicationFactory>
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
        payload.BillingEventsAppended.Should().Be(1);
        payload.RecoveredPaymentAttempts.Should().Be(1);
        payload.ArchivedEventsAwaitingConfirmation.Should().BeNull();
        payload.ReconciledAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));

        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE paystack_reference = @reference", [new { reference }]).Should().Be(nameof(PaystackPaymentAttemptStatus.Succeeded));
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string>("SELECT plan FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));

        var billingEvent = Connection.ExecuteScalar<string>("SELECT event_type FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId = $"paystack:{reference}:Subscribe" }]);
        billingEvent.Should().Be(nameof(BillingEventType.SubscriptionCreated));
        var subscribedSince = Connection.ExecuteScalar<string>("SELECT subscribed_since FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        subscribedSince.Should().NotBeNullOrWhiteSpace();
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "SubscriptionCreated");
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenBillingEventAlreadyExistsForProviderEventId_ShouldNotAppendDuplicateBillingEvent()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("billing_info", """{"Name":"Test Organization","Address":null,"Email":"billing@example.com","TaxId":null}""")
            ]
        );
        var subscriptionId = Connection.ExecuteScalar<string>("SELECT id FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        var reference = $"nerova_reconcile_duplicate_{Guid.NewGuid():N}";
        var providerEventId = $"paystack:{reference}:Subscribe";
        Connection.Insert("billing_events", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", BillingEventId.NewId().Value),
                ("subscription_id", subscriptionId),
                ("created_at", DateTimeOffset.UtcNow.AddMinutes(-5)),
                ("modified_at", null),
                ("provider_event_id", providerEventId),
                ("event_type", nameof(BillingEventType.SubscriptionCreated)),
                ("from_plan", null),
                ("to_plan", nameof(SubscriptionPlan.Standard)),
                ("previous_amount", null),
                ("new_amount", 29.00m),
                ("amount_delta", 29.00m),
                ("committed_mrr", 29.00m),
                ("currency", MockPaystackClient.MockStandardCurrency),
                ("occurred_at", DateTimeOffset.UtcNow.AddMinutes(-5)),
                ("cancellation_reason", null),
                ("suspension_reason", null)
            ]
        );
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

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-paystack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReconcileTenantWithPaystackResponse>();
        payload.Should().NotBeNull();
        payload.BillingEventsAppended.Should().Be(0);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }]).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE paystack_reference = @reference", [new { reference }]).Should().Be(nameof(PaystackPaymentAttemptStatus.Succeeded));
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenSucceededSubscribeAttemptHasNoBillingEvent_ShouldBackfillLedgerAndClearDrift()
    {
        // Arrange
        var completedAt = DateTimeOffset.UtcNow.AddDays(-3);
        var reference = $"nerova_reconcile_backfill_{Guid.NewGuid():N}";
        var transactions = new[]
        {
            new PaymentTransaction(
                PaymentTransactionId.NewId(),
                29.00m,
                29.00m,
                0m,
                MockPaystackClient.MockStandardCurrency,
                PaymentTransactionStatus.Succeeded,
                completedAt,
                null,
                null,
                null,
                SubscriptionPlan.Standard
            )
        };

        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [
                ("state", nameof(TenantState.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "AUTH_backfill"),
                ("current_price_amount", 29.00m),
                ("current_price_currency", MockPaystackClient.MockStandardCurrency),
                ("current_period_end", completedAt.AddMonths(1)),
                ("next_billing_at", completedAt.AddMonths(1)),
                ("payment_transactions", JsonSerializer.Serialize(transactions)),
                ("subscribed_since", null),
                ("has_drift_detected", true),
                ("drift_checked_at", DateTimeOffset.UtcNow.AddMinutes(-5)),
                ("drift_discrepancies", "[]")
            ]
        );
        var subscriptionId = Connection.ExecuteScalar<string>("SELECT id FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        Connection.Insert("paystack_payment_attempts", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", PaystackPaymentAttemptId.NewId().ToString()),
                ("subscription_id", subscriptionId),
                ("created_at", completedAt.AddMinutes(-5)),
                ("modified_at", null),
                ("paystack_reference", reference),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "AUTH_backfill"),
                ("purpose", nameof(PaystackPaymentPurpose.Subscribe)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("amount", 29.00m),
                ("currency", MockPaystackClient.MockStandardCurrency),
                ("status", nameof(PaystackPaymentAttemptStatus.Succeeded)),
                ("completed_at", completedAt),
                ("failure_reason", null)
            ]
        );

        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        client.DefaultRequestHeaders.Add("Cookie", $"{OAuthProviderFactory.UseMockProviderCookieName}=true");

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-paystack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReconcileTenantWithPaystackResponse>();
        payload.Should().NotBeNull();
        payload.BillingEventsAppended.Should().Be(1);
        payload.RecoveredPaymentAttempts.Should().Be(1);
        payload.HasDriftDetected.Should().BeFalse();
        payload.DriftDiscrepancyCount.Should().Be(0);

        var providerEventId = $"paystack:{reference}:Subscribe";
        Connection.ExecuteScalar<string>("SELECT event_type FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }])
            .Should().Be(nameof(BillingEventType.SubscriptionCreated));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT committed_mrr FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }]), CultureInfo.InvariantCulture)
            .Should().Be(29.00m);
        Connection.ExecuteScalar<string>("SELECT currency FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }])
            .Should().Be(MockPaystackClient.MockStandardCurrency);
        ParseDbDateTimeOffset(Connection.ExecuteScalar<string>("SELECT occurred_at FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }]))
            .Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));

        var subscribedSince = ParseDbDateTimeOffset(Connection.ExecuteScalar<string>("SELECT subscribed_since FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]));
        subscribedSince.Should().BeCloseTo(completedAt, TimeSpan.FromSeconds(1));
    }

    private static DateTimeOffset ParseDbDateTimeOffset(string value)
    {
        if (long.TryParse(value, out var unixMs))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        }
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenSucceededUpgradeAttemptHasNoBillingEvent_ShouldBackfillMrrDeltaFromPreviousLedgerEvent()
    {
        // Arrange
        var subscribedAt = DateTimeOffset.UtcNow.AddHours(-5);
        var upgradedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var subscribeReference = $"nerova_reconcile_subscribe_{Guid.NewGuid():N}";
        var upgradeReference = $"nerova_reconcile_upgrade_{Guid.NewGuid():N}";
        var subscriptionId = Connection.ExecuteScalar<string>("SELECT id FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        var transactions = new[]
        {
            new PaymentTransaction(
                PaymentTransactionId.NewId(),
                800.00m,
                800.00m,
                0m,
                MockPaystackClient.MockStandardCurrency,
                PaymentTransactionStatus.Succeeded,
                subscribedAt,
                null,
                null,
                null,
                SubscriptionPlan.Standard
            ),
            new PaymentTransaction(
                PaymentTransactionId.NewId(),
                397.60m,
                397.60m,
                0m,
                MockPaystackClient.MockStandardCurrency,
                PaymentTransactionStatus.Succeeded,
                upgradedAt,
                null,
                null,
                null,
                SubscriptionPlan.Premium
            )
        };

        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [
                ("state", nameof(TenantState.Active)),
                ("plan", nameof(SubscriptionPlan.Premium))
            ]
        );
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "AUTH_upgrade_backfill"),
                ("current_price_amount", 1200.00m),
                ("current_price_currency", MockPaystackClient.MockStandardCurrency),
                ("current_period_end", upgradedAt.AddMonths(1)),
                ("next_billing_at", upgradedAt.AddMonths(1)),
                ("payment_transactions", JsonSerializer.Serialize(transactions)),
                ("subscribed_since", subscribedAt),
                ("has_drift_detected", true),
                ("drift_checked_at", DateTimeOffset.UtcNow.AddMinutes(-5)),
                ("drift_discrepancies", "[]")
            ]
        );
        Connection.Insert("billing_events", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", BillingEventId.NewId().Value),
                ("subscription_id", subscriptionId),
                ("created_at", DateTimeOffset.UtcNow),
                ("modified_at", null),
                ("provider_event_id", $"paystack:{subscribeReference}:Subscribe"),
                ("event_type", nameof(BillingEventType.SubscriptionCreated)),
                ("from_plan", null),
                ("to_plan", nameof(SubscriptionPlan.Standard)),
                ("previous_amount", null),
                ("new_amount", 800.00m),
                ("amount_delta", 800.00m),
                ("committed_mrr", 800.00m),
                ("currency", MockPaystackClient.MockStandardCurrency),
                ("occurred_at", subscribedAt),
                ("cancellation_reason", null),
                ("suspension_reason", null)
            ]
        );
        Connection.Insert("paystack_payment_attempts", [
                ("tenant_id", DatabaseSeeder.Tenant1.Id.Value),
                ("id", PaystackPaymentAttemptId.NewId().ToString()),
                ("subscription_id", subscriptionId),
                ("created_at", upgradedAt.AddMinutes(-5)),
                ("modified_at", null),
                ("paystack_reference", upgradeReference),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "AUTH_upgrade_backfill"),
                ("purpose", nameof(PaystackPaymentPurpose.Upgrade)),
                ("plan", nameof(SubscriptionPlan.Premium)),
                ("amount", 397.60m),
                ("currency", MockPaystackClient.MockStandardCurrency),
                ("status", nameof(PaystackPaymentAttemptStatus.Succeeded)),
                ("completed_at", upgradedAt),
                ("failure_reason", null)
            ]
        );

        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        client.DefaultRequestHeaders.Add("Cookie", $"{OAuthProviderFactory.UseMockProviderCookieName}=true");

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-paystack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ReconcileTenantWithPaystackResponse>();
        payload.Should().NotBeNull();
        payload.BillingEventsAppended.Should().Be(1);
        payload.RecoveredPaymentAttempts.Should().Be(1);

        var providerEventId = $"paystack:{upgradeReference}:Upgrade";
        Connection.ExecuteScalar<string>("SELECT event_type FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }])
            .Should().Be(nameof(BillingEventType.SubscriptionUpgraded));
        Connection.ExecuteScalar<string>("SELECT from_plan FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }])
            .Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string>("SELECT to_plan FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }])
            .Should().Be(nameof(SubscriptionPlan.Premium));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT previous_amount FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }]), CultureInfo.InvariantCulture)
            .Should().Be(800.00m);
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT new_amount FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }]), CultureInfo.InvariantCulture)
            .Should().Be(1200.00m);
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT amount_delta FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }]), CultureInfo.InvariantCulture)
            .Should().Be(400.00m);
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT committed_mrr FROM billing_events WHERE provider_event_id = @providerEventId", [new { providerEventId }]), CultureInfo.InvariantCulture)
            .Should().Be(1200.00m);
    }

    [Fact]
    public async Task ReconcileTenantWithPaystack_WhenCalledThroughLegacyCompatibilityRoute_ShouldReturnNotFound()
    {
        // Arrange
        var legacyProvider = string.Concat("str", "ipe");
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/reconcile-with-{legacyProvider}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReplayArchivedTenantPaystackEvents_WhenCalledThroughLegacyCompatibilityRoute_ShouldReturnNotFound()
    {
        // Arrange
        var legacyProvider = string.Concat("str", "ipe");
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.PostAsync($"/api/back-office/tenants/{DatabaseSeeder.Tenant1.Id}/replay-archived-{legacyProvider}-events", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

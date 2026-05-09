using System.Globalization;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Features.Tenants.Domain;
using Account.Integrations.OAuth;
using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class ProcessSubscriptionBillingTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ProcessSubscriptionBilling_WhenRenewalDue_ShouldChargeAuthorizationAndAdvancePeriod()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(SubscriptionPlan.Standard, 29.00m, now.AddMonths(-1), now.AddDays(-1), now.AddDays(-1));

        // Act
        await ProcessBillingAsync();

        // Assert
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT current_price_amount FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(29.00m);
        Connection.ExecuteScalar<string?>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().BeNull();
        DateTimeOffset.Parse(Connection.ExecuteScalar<string>("SELECT next_billing_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().BeAfter(now.AddDays(25));
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM paystack_payment_attempts WHERE purpose = @purpose AND status = @status", [new { purpose = nameof(PaystackPaymentPurpose.Renewal), status = nameof(PaystackPaymentAttemptStatus.Succeeded) }]).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT payment_transactions FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Contain("\"Status\":\"Succeeded\"");
    }

    [Fact]
    public async Task ProcessSubscriptionBilling_WhenRenewalChargeFails_ShouldKeepPaidAccessDuringGracePeriod()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(SubscriptionPlan.Standard, 29.00m, now.AddMonths(-1), now.AddDays(-1), now.AddDays(-1));

        // Act
        await ProcessBillingAsync(state => state.SimulateAuthorizationChargeFailure = true);

        // Assert
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(TenantState.Active));
        Connection.ExecuteScalar<string?>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().NotBeNull();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM paystack_payment_attempts WHERE purpose = @purpose AND status = @status", [new { purpose = nameof(PaystackPaymentPurpose.Renewal), status = nameof(PaystackPaymentAttemptStatus.Failed) }]).Should().Be(1);
    }

    [Fact]
    public async Task ProcessSubscriptionBilling_WhenFailedRenewalExceedsGracePeriod_ShouldSuspendTenantAndResetSubscription()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(
            SubscriptionPlan.Standard,
            29.00m,
            now.AddMonths(-1).AddDays(-8),
            now.AddDays(-8),
            now.AddDays(-8),
            firstPaymentFailedAt: now.AddDays(-8)
        );

        // Act
        await ProcessBillingAsync();

        // Assert
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Basis));
        Connection.ExecuteScalar<string?>("SELECT paystack_authorization_code FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().BeNull();
        Connection.ExecuteScalar<string>("SELECT state FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(TenantState.Suspended));
        Connection.ExecuteScalar<string>("SELECT suspension_reason FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SuspensionReason.PaymentFailed));
    }

    [Fact]
    public async Task ProcessSubscriptionBilling_WhenCancellationReachedPeriodEnd_ShouldExpireToBasis()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(
            SubscriptionPlan.Standard,
            29.00m,
            now.AddMonths(-1),
            now.AddDays(-1),
            now.AddDays(-1),
            cancelAtPeriodEnd: true
        );

        // Act
        await ProcessBillingAsync();

        // Assert
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Basis));
        Connection.ExecuteScalar<string>("SELECT plan FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Basis));
        Connection.ExecuteScalar<long>("SELECT cancel_at_period_end FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(0);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM paystack_payment_attempts WHERE purpose = @purpose", [new { purpose = nameof(PaystackPaymentPurpose.Renewal) }]).Should().Be(0);
    }

    [Fact]
    public async Task ProcessSubscriptionBilling_WhenScheduledDowngradeRenewalDue_ShouldApplyDowngradeAndChargeLowerPlan()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        SaveActiveSubscription(
            SubscriptionPlan.Premium,
            99.00m,
            now.AddMonths(-1),
            now.AddDays(-1),
            now.AddDays(-1),
            SubscriptionPlan.Standard
        );

        // Act
        await ProcessBillingAsync();

        // Assert
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string?>("SELECT scheduled_plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().BeNull();
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT current_price_amount FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(29.00m);
        Connection.ExecuteScalar<string>("SELECT plan FROM tenants WHERE id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT amount FROM paystack_payment_attempts WHERE purpose = @purpose", [new { purpose = nameof(PaystackPaymentPurpose.Renewal) }]), CultureInfo.InvariantCulture).Should().Be(29.00m);
    }

    private async Task ProcessBillingAsync(Action<MockPaystackState>? configureState = null)
    {
        using var scope = Provider.CreateScope();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext();
        httpContextAccessor.HttpContext.Request.Headers.Cookie = $"{OAuthProviderFactory.UseMockProviderCookieName}=true";
        configureState?.Invoke(scope.ServiceProvider.GetRequiredService<MockPaystackState>());

        var processor = scope.ServiceProvider.GetRequiredService<ProcessSubscriptionBilling>();
        await processor.ExecuteAsync(CancellationToken.None);
    }

    private void SaveActiveSubscription(
        SubscriptionPlan plan,
        decimal currentPriceAmount,
        DateTimeOffset currentPeriodStart,
        DateTimeOffset currentPeriodEnd,
        DateTimeOffset nextBillingAt,
        SubscriptionPlan? scheduledPlan = null,
        DateTimeOffset? firstPaymentFailedAt = null,
        bool cancelAtPeriodEnd = false
    )
    {
        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [
                ("state", nameof(TenantState.Active)),
                ("plan", plan.ToString()),
                ("suspension_reason", null),
                ("suspended_at", null)
            ]
        );

        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", plan.ToString()),
                ("scheduled_plan", scheduledPlan?.ToString()),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", MockPaystackClient.MockAuthorizationCode),
                ("paystack_authorization_email", "billing@example.com"),
                ("paystack_authorization_signature", "SIG_mock_12345"),
                ("current_price_amount", currentPriceAmount),
                ("current_price_currency", "USD"),
                ("current_period_start", currentPeriodStart),
                ("current_period_end", currentPeriodEnd),
                ("next_billing_at", nextBillingAt),
                ("cancel_at_period_end", cancelAtPeriodEnd),
                ("first_payment_failed_at", firstPaymentFailedAt),
                ("payment_method", """{"Brand":"visa","Last4":"4242","ExpMonth":12,"ExpYear":2026}"""),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
    }
}

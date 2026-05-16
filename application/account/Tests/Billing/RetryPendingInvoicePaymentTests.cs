using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Billing.Commands;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Billing;

public sealed class RetryPendingInvoicePaymentTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task RetryRenewalPayment_WhenAuthorizationChargePaid_ShouldReturnPaid()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "sub_test_123"),
                ("paystack_authorization_email", DatabaseSeeder.Tenant1Owner.Email),
                ("current_price_amount", 29.99m),
                ("current_price_currency", "USD"),
                ("first_payment_failed_at", TimeProvider.GetUtcNow().AddDays(-1)),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/retry-renewal-payment", null);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RetryRenewalPaymentResponse>();
        result!.Paid.Should().BeTrue();
        result.Reference.Should().NotBeNullOrEmpty();
        result.AccessCode.Should().BeNull();
        result.OperationPurpose.Should().Be("Retry");
        Connection.ExecuteScalar<string>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().BeNull();
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE paystack_reference = @reference", [new { reference = result.Reference }]).Should().Be("Succeeded");
        var transactions = Connection.ExecuteScalar<string>("SELECT payment_transactions FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        transactions.Should().Contain("\"Amount\":29.99");
        transactions.Should().Contain("\"Status\":\"Succeeded\"");
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("RenewalPaymentRetried");
    }

    [Fact]
    public async Task RetryRenewalPayment_WhenUsingCompatibilityInvoiceRoute_ShouldReturnPaid()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "AUTH_test_123"),
                ("paystack_authorization_email", DatabaseSeeder.Tenant1Owner.Email),
                ("current_price_amount", 29.99m),
                ("current_price_currency", "USD"),
                ("first_payment_failed_at", TimeProvider.GetUtcNow().AddDays(-1)),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/retry-pending-invoice", null);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RetryRenewalPaymentResponse>();
        result!.Paid.Should().BeTrue();
    }

    [Fact]
    public async Task RetryRenewalPayment_WhenAuthorizationChargeFails_ShouldReturnBadRequestAndKeepPaymentFailure()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "sub_test_123"),
                ("paystack_authorization_email", DatabaseSeeder.Tenant1Owner.Email),
                ("current_price_amount", 29.99m),
                ("current_price_currency", "USD"),
                ("first_payment_failed_at", TimeProvider.GetUtcNow().AddDays(-1)),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        PaystackState.SimulateAuthorizationChargeFailure = true;

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/retry-renewal-payment", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Paystack could not charge the saved payment method.");
        Connection.ExecuteScalar<string?>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().NotBeNull();
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE purpose = @purpose", [new { purpose = nameof(PaystackPaymentPurpose.Retry) }]).Should().Be("Failed");
    }

    [Fact]
    public async Task RetryRenewalPayment_WhenNoPendingRenewalPayment_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", "cus_test_123"),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/retry-renewal-payment", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No pending renewal payment found for this subscription.");
    }

    [Fact]
    public async Task RetryRenewalPayment_WhenNonOwner_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsync("/api/account/billing/retry-renewal-payment", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");
    }

    [Fact]
    public async Task RetryRenewalPayment_WhenNoPaystackAuthorization_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", "cus_test_123")
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/retry-renewal-payment", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No active Paystack authorization found.");
    }
}

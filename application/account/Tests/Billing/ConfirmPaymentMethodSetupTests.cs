using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Billing.Commands;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Billing;

public sealed class ConfirmPaymentMethodSetupTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenValid_ShouldSucceed()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30)),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var setupResponse = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/start-payment-method-setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = await setupResponse.Content.ReadFromJsonAsync<StartPaymentMethodSetupResponse>();
        var command = new ConfirmPaymentMethodSetupCommand(setup!.Reference);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfirmPaymentMethodSetupResponse>();
        result!.HasPendingRenewalPayment.Should().BeFalse();
        result.PendingRenewalPaymentAmount.Should().BeNull();
        result.PendingRenewalPaymentCurrency.Should().BeNull();
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE paystack_reference = @reference", [new { reference = setup.Reference }]).Should().Be("Succeeded");
        Connection.ExecuteScalar<string>("SELECT paystack_authorization_code FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(MockPaystackClient.MockAuthorizationCode);
        var transactions = Connection.ExecuteScalar<string>("SELECT payment_transactions FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        transactions.Should().Contain("\"Amount\":1.00");
        transactions.Should().Contain("\"Status\":\"Refunded\"");
    }

    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        var command = new ConfirmPaymentMethodSetupCommand("seti_mock_12345");

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");
    }

    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenNoPaystackCustomer_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new ConfirmPaymentMethodSetupCommand("seti_mock_12345");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "No Paystack customer found. A subscription must be created first.");
    }

    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenNoPaystackAuthorization_ShouldSetCustomerDefault()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var setupResponse = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/start-payment-method-setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = await setupResponse.Content.ReadFromJsonAsync<StartPaymentMethodSetupResponse>();
        var command = new ConfirmPaymentMethodSetupCommand(setup!.Reference);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfirmPaymentMethodSetupResponse>();
        result!.HasPendingRenewalPayment.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenRefundFails_ShouldKeepPreviousAuthorization()
    {
        // Arrange
        const string previousAuthorizationCode = "AUTH_previous_123";
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", previousAuthorizationCode),
                ("paystack_authorization_email", "previous@example.com"),
                ("paystack_authorization_signature", "SIG_previous"),
                ("payment_method", """{"Brand":"visa","Last4":"1111","ExpMonth":12,"ExpYear":2026}"""),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30)),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var setupResponse = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/start-payment-method-setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = await setupResponse.Content.ReadFromJsonAsync<StartPaymentMethodSetupResponse>();
        PaystackState.SimulateRefundFailure = true;

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", new ConfirmPaymentMethodSetupCommand(setup!.Reference));

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Failed to refund Paystack payment method authorization charge.");
        Connection.ExecuteScalar<string>("SELECT paystack_authorization_code FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(previousAuthorizationCode);
        Connection.ExecuteScalar<string>("SELECT status FROM paystack_payment_attempts WHERE paystack_reference = @reference", [new { reference = setup.Reference }]).Should().Be("Failed");
    }

    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenNoPaymentAttemptExists_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode)
            ]
        );
        var command = new ConfirmPaymentMethodSetupCommand("seti_mock_12345");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Paystack payment method authorization attempt was not found.");
    }

    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenReferenceIsSubscriptionPayment_ShouldReturnBadRequest()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        var checkoutResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/start-checkout", new StartSubscriptionCheckoutCommand(SubscriptionPlan.Standard));
        checkoutResponse.EnsureSuccessStatusCode();
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<StartSubscriptionCheckoutResponse>();
        var command = new ConfirmPaymentMethodSetupCommand(checkout!.Reference!);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Only Paystack payment method authorizations can be confirmed here.");
    }
}

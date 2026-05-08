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

public sealed class ConfirmPaymentMethodSetupTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ConfirmPaymentMethodSetup_WhenValid_ShouldSucceed()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", "sub_test_123"),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30))
            ]
        );
        var command = new ConfirmPaymentMethodSetupCommand("seti_mock_12345");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/billing/confirm-payment-method", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfirmPaymentMethodSetupResponse>();
        result!.HasOpenInvoice.Should().BeFalse();
        result.OpenInvoiceAmount.Should().BeNull();
        result.OpenInvoiceCurrency.Should().BeNull();
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
    public async Task ConfirmPaymentMethodSetup_WhenNoPaystackSubscription_ShouldSetCustomerDefault()
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
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfirmPaymentMethodSetupResponse>();
        result!.HasOpenInvoice.Should().BeFalse();
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

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class ConfirmPaystackPaymentTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task ConfirmPaystackPayment_WhenStandardSubscribePaymentVerified_ShouldActivateSubscription()
    {
        // Arrange
        SaveBillingInfo();
        var command = new ConfirmPaystackPaymentCommand(MockPaystackClient.MockReference, SubscriptionPlan.Standard, PaystackPaymentPurpose.Subscribe);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/confirm-payment", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfirmPaystackPaymentResponse>();
        result!.Paid.Should().BeTrue();
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Standard));
        Connection.ExecuteScalar<string>("SELECT paystack_authorization_code FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(MockPaystackClient.MockAuthorizationCode);
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT current_price_amount FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(29.00m);
        Connection.ExecuteScalar<string>("SELECT current_price_currency FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be("USD");

        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionCreated");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmPaystackPayment_WhenPremiumSavedPaymentMethodPaymentVerified_ShouldActivateSubscription()
    {
        // Arrange
        SaveBillingInfoWithSavedPaymentMethod();
        var checkoutResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/start-checkout", new StartSubscriptionCheckoutCommand(SubscriptionPlan.Premium));
        checkoutResponse.EnsureSuccessStatusCode();
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<StartSubscriptionCheckoutResponse>();
        var command = new ConfirmPaystackPaymentCommand(checkout!.Reference!, SubscriptionPlan.Premium, PaystackPaymentPurpose.Subscribe);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/confirm-payment", command);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConfirmPaystackPaymentResponse>();
        result!.Paid.Should().BeTrue();
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Premium));
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT current_price_amount FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]), CultureInfo.InvariantCulture).Should().Be(99.00m);
    }

    [Fact]
    public async Task ConfirmPaystackPayment_WhenPurposeIsNotSubscriptionPayment_ShouldReturnBadRequest()
    {
        // Arrange
        SaveBillingInfo();
        var command = new ConfirmPaystackPaymentCommand(MockPaystackClient.MockReference, SubscriptionPlan.Standard, PaystackPaymentPurpose.PaymentMethodAuthorization);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/confirm-payment", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Only subscription Paystack payments can be confirmed here.");
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Basis));

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmPaystackPayment_WhenVerifiedAmountDoesNotMatchPlan_ShouldReturnBadRequest()
    {
        // Arrange
        SaveBillingInfo();
        var command = new ConfirmPaystackPaymentCommand(MockPaystackClient.MockReference, SubscriptionPlan.Premium, PaystackPaymentPurpose.Subscribe);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/account/subscriptions/confirm-payment", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Paystack payment amount does not match the expected subscription amount.");
        Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]).Should().Be(nameof(SubscriptionPlan.Basis));

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmPaystackPayment_WhenNonOwner_ShouldReturnForbidden()
    {
        // Arrange
        SaveBillingInfo();
        var command = new ConfirmPaystackPaymentCommand(MockPaystackClient.MockReference, SubscriptionPlan.Standard, PaystackPaymentPurpose.Subscribe);
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/account/subscriptions/confirm-payment", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can manage subscriptions.");

        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }

    private void SaveBillingInfo()
    {
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
    }

    private void SaveBillingInfoWithSavedPaymentMethod()
    {
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", MockPaystackClient.MockAuthorizationCode),
                ("paystack_authorization_email", "billing@example.com"),
                ("paystack_authorization_signature", "SIG_mock_12345"),
                ("payment_method", """{"Brand":"visa","Last4":"4242","ExpMonth":12,"ExpYear":2026}"""),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
    }
}

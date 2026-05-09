using System.Net.Http.Json;
using Account.Database;
using Account.Features.Billing.Commands;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Billing;

public sealed class StartPaymentMethodSetupTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task StartPaymentMethodSetup_WhenValid_ShouldReturnSetupAndPersistPaymentAttempt()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("billing_info", """{"Name":"Test Organization","Address":{"Line1":"Vestergade 12","PostalCode":"1456","City":"Copenhagen","Country":"DK"},"Email":"billing@example.com"}""")
            ]
        );
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/account/billing/start-payment-method-setup", null);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StartPaymentMethodSetupResponse>();
        result!.AccessCode.Should().NotBeNullOrEmpty();
        result.Reference.Should().NotBeNullOrEmpty();
        result.PublicKey.Should().NotBeNullOrEmpty();
        result.Amount.Should().Be(1.00m);
        result.Currency.Should().Be("USD");
        result.OperationPurpose.Should().Be(nameof(PaystackPaymentPurpose.PaymentMethodAuthorization));
        Connection.ExecuteScalar<long>(
            """
            SELECT COUNT(*)
            FROM paystack_payment_attempts
            WHERE tenant_id = @tenantId
              AND subscription_id = (SELECT id FROM subscriptions WHERE tenant_id = @tenantId)
              AND paystack_reference = @reference
              AND paystack_customer_code = @customerCode
              AND purpose = @purpose
              AND plan IS NULL
              AND status = @status
              AND amount = @amount
              AND currency = @currency
            """,
            [
                new
                {
                    tenantId = DatabaseSeeder.Tenant1.Id.Value,
                    reference = result.Reference,
                    customerCode = MockPaystackClient.MockCustomerCode,
                    purpose = nameof(PaystackPaymentPurpose.PaymentMethodAuthorization),
                    status = "Pending",
                    amount = result.Amount,
                    currency = result.Currency
                }
            ]
        ).Should().Be(1);

        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("PaymentMethodSetupStarted");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeTrue();
    }
}

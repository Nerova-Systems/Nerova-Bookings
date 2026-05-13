using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Account.Features.BackOffice.Invoices.Queries;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Integrations.OAuth;
using Account.Integrations.Paystack;
using FluentAssertions;
using SharedKernel.Authentication.MockEasyAuth;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.BackOffice;

public sealed class RefundBackOfficeInvoiceTests : BackOfficeEndpointBaseTest
{
    [Fact]
    public async Task RefundBackOfficeInvoice_WhenPaidInvoiceHasPaystackReference_ShouldRefundAndKeepMrrUnchanged()
    {
        // Arrange
        var transactionId = PaymentTransactionId.NewId();
        var paystackReference = $"nerova_invoice_refund_{Guid.NewGuid():N}";
        var paidAt = DateTimeOffset.UtcNow.AddDays(-2);
        var subscriptionId = Connection.ExecuteScalar<string>("SELECT id FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        Connection.Update("tenants", "id", DatabaseSeeder.Tenant1.Id.Value, [
                ("state", nameof(TenantState.Active)),
                ("plan", nameof(SubscriptionPlan.Standard))
            ]
        );
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("paystack_authorization_code", MockPaystackClient.MockAuthorizationCode),
                ("current_price_amount", 29.00m),
                ("current_price_currency", MockPaystackClient.MockStandardCurrency),
                ("payment_transactions", $$"""
                                          [
                                            {
                                              "Id": "{{transactionId}}",
                                              "Amount": 29.00,
                                              "AmountExcludingTax": 29.00,
                                              "TaxAmount": 0.00,
                                              "Currency": "{{MockPaystackClient.MockStandardCurrency}}",
                                              "Status": "Succeeded",
                                              "Date": "{{paidAt:O}}",
                                              "FailureReason": null,
                                              "InvoiceUrl": "https://paystack.test/invoice/{{paystackReference}}",
                                              "CreditNoteUrl": null,
                                              "Plan": "Standard",
                                              "RefundedAt": null,
                                              "InvoiceTotal": 29.00,
                                              "AmountFromCredit": 0.00,
                                              "CreditNotedAt": null,
                                              "PaystackReference": "{{paystackReference}}"
                                            }
                                          ]
                                          """)
            ]
        );

        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        client.DefaultRequestHeaders.Add("Cookie", $"{OAuthProviderFactory.UseMockProviderCookieName}=true");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await client.PostAsync($"/api/back-office/invoices/{transactionId}/refund", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var invoicesResponse = await client.GetAsync("/api/back-office/invoices?Statuses=Refunded&PageSize=10");
        invoicesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var invoices = await invoicesResponse.Content.ReadFromJsonAsync<BackOfficeInvoicesResponse>();
        invoices.Should().NotBeNull();
        invoices.Invoices.Should().ContainSingle(i => i.Id == transactionId && i.RowKind == BackOfficeInvoiceRowKind.Refund && i.Status == PaymentTransactionStatus.Refunded);

        var paymentTransactions = Connection.ExecuteScalar<string>("SELECT payment_transactions FROM subscriptions WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.Tenant1.Id.Value }]);
        paymentTransactions.Should().Contain("\"Status\":\"Refunded\"");
        paymentTransactions.Should().Contain("\"PaystackReference\":\"" + paystackReference + "\"");

        Connection.ExecuteScalar<string>("SELECT event_type FROM billing_events WHERE subscription_id = @subscriptionId", [new { subscriptionId }]).Should().Be(nameof(BillingEventType.PaymentRefunded));
        Connection.ExecuteScalar<object?>("SELECT amount_delta FROM billing_events WHERE subscription_id = @subscriptionId", [new { subscriptionId }]).Should().BeNull();
        decimal.Parse(Connection.ExecuteScalar<string>("SELECT committed_mrr FROM billing_events WHERE subscription_id = @subscriptionId", [new { subscriptionId }]), CultureInfo.InvariantCulture).Should().Be(29.00m);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e.GetType().Name == "PaymentRefunded");
    }

    [Fact]
    public async Task RefundBackOfficeInvoice_WhenInvoiceIsAlreadyRefunded_ShouldReturnBadRequest()
    {
        // Arrange
        var transactionId = PaymentTransactionId.NewId();
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("payment_transactions", $$"""
                                          [
                                            {
                                              "Id": "{{transactionId}}",
                                              "Amount": 29.00,
                                              "AmountExcludingTax": 29.00,
                                              "TaxAmount": 0.00,
                                              "Currency": "{{MockPaystackClient.MockStandardCurrency}}",
                                              "Status": "Refunded",
                                              "Date": "{{DateTimeOffset.UtcNow.AddDays(-2):O}}",
                                              "FailureReason": null,
                                              "InvoiceUrl": null,
                                              "CreditNoteUrl": null,
                                              "Plan": "Standard",
                                              "RefundedAt": "{{DateTimeOffset.UtcNow.AddDays(-1):O}}",
                                              "InvoiceTotal": 29.00,
                                              "AmountFromCredit": 0.00,
                                              "CreditNotedAt": null,
                                              "PaystackReference": "nerova_already_refunded"
                                            }
                                          ]
                                          """)
            ]
        );

        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "admin");
        using var client = CreateBackOfficeClientForIdentity(identity);
        client.DefaultRequestHeaders.Add("Cookie", $"{OAuthProviderFactory.UseMockProviderCookieName}=true");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await client.PostAsync($"/api/back-office/invoices/{transactionId}/refund", null);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "Invoice is already refunded.");
        TelemetryEventsCollectorSpy.AreAllEventsDispatched.Should().BeFalse();
    }
}

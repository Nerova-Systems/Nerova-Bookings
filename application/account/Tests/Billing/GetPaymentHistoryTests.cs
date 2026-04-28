using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Billing.Queries;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Billing;

public sealed class GetPaymentHistoryTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task GetPaymentHistory_WhenTransactionsExist_ShouldReturnPaginatedHistory()
    {
        // Arrange
        var transactionId = PaymentTransactionId.NewId().ToString();
        var transactionsJson = $$"""[{"Id":"{{transactionId}}","Amount":29.99,"Currency":"usd","Status":"Succeeded","Date":"2026-01-01T00:00:00+00:00","FailureReason":null,"InvoiceUrl":"https://invoice.test/123","Provider":"PayFast","ProviderPaymentId":"pf-123","ProviderStatus":"COMPLETE","RefundedAmount":10.00}]""";
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("payment_transactions", transactionsJson)
            ]
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/billing/payment-history");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.Content.ReadFromJsonAsync<PaymentHistoryResponse>();
        result!.TotalCount.Should().Be(1);
        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].Amount.Should().Be(29.99m);
        result.Transactions[0].Currency.Should().Be("usd");
        result.Transactions[0].Status.Should().Be(PaymentTransactionStatus.Succeeded);
        result.Transactions[0].Provider.Should().Be("PayFast");
        result.Transactions[0].ProviderPaymentId.Should().Be("pf-123");
        result.Transactions[0].RefundedAmount.Should().Be(10.00m);
        result.Transactions[0].RefundStatus.Should().Be("PartiallyRefunded");
    }

    [Fact]
    public async Task GetPaymentHistory_WhenNotOwner_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/account/billing/payment-history");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

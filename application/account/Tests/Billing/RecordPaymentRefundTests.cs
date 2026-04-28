using System.Text.Json;
using Account.Database;
using Account.Features.Billing.Commands;
using Account.Features.Subscriptions.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Billing;

public sealed class RecordPaymentRefundTests : EndpointBaseTest<AccountDbContext>
{
    [Fact]
    public async Task RecordPaymentRefund_WhenManualRefund_ShouldUpdateTransactionRefundState()
    {
        // Arrange
        var transactionId = PaymentTransactionId.NewId();
        var transactionsJson = $$"""[{"Id":"{{transactionId}}","Amount":100.00,"Currency":"zar","Status":"Succeeded","Date":"2026-01-01T00:00:00+00:00","FailureReason":null,"InvoiceUrl":null,"Provider":"PayFast","ProviderPaymentId":"pf-123","RefundedAmount":0}]""";
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("payment_transactions", transactionsJson)
            ]
        );

        using var scope = Provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new RecordPaymentRefundCommand(transactionId, 40m, "Support credit", "https://credit-note.test/1", "manual-ref-1"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.RefundedAmount.Should().Be(40m);
        result.Value.Status.Should().Be(PaymentTransactionStatus.Succeeded);

        var updatedJson = Connection.ExecuteScalar<string>("SELECT payment_transactions FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        using var document = JsonDocument.Parse(updatedJson);
        var transaction = document.RootElement[0];
        transaction.GetProperty("RefundedAmount").GetDecimal().Should().Be(40m);
        transaction.GetProperty("CreditNoteUrl").GetString().Should().Be("https://credit-note.test/1");
    }
}

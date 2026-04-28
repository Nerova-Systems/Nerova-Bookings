using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Integrations.PayFast;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Telemetry;

namespace Account.Features.Billing.Commands;

[PublicAPI]
public sealed record RecordPaymentRefundCommand(
    PaymentTransactionId TransactionId,
    decimal Amount,
    string Reason,
    string? CreditNoteUrl,
    string? PayFastReference,
    bool ProcessWithPayFast = false
) : ICommand, IRequest<Result<RecordPaymentRefundResponse>>;

[PublicAPI]
public sealed record RecordPaymentRefundResponse(
    PaymentTransactionId TransactionId,
    decimal RefundedAmount,
    PaymentTransactionStatus Status,
    string? RefundReference
);

public sealed class RecordPaymentRefundHandler(
    ISubscriptionRepository subscriptionRepository,
    IPayFastClient payFastClient,
    ITelemetryEventsCollector events
) : IRequestHandler<RecordPaymentRefundCommand, Result<RecordPaymentRefundResponse>>
{
    public async Task<Result<RecordPaymentRefundResponse>> Handle(RecordPaymentRefundCommand command, CancellationToken cancellationToken)
    {
        if (command.Amount <= 0)
        {
            return Result<RecordPaymentRefundResponse>.BadRequest("Refund amount must be greater than zero.");
        }

        var subscription = await subscriptionRepository.GetByPaymentTransactionIdUnfilteredAsync(command.TransactionId, cancellationToken);
        if (subscription is null)
        {
            return Result<RecordPaymentRefundResponse>.NotFound("Payment transaction was not found.");
        }

        var transaction = subscription.PaymentTransactions.Single(t => t.Id == command.TransactionId);
        if (transaction.Status is not (PaymentTransactionStatus.Succeeded or PaymentTransactionStatus.Refunded))
        {
            return Result<RecordPaymentRefundResponse>.BadRequest("Only successful transactions can be refunded.");
        }

        if (transaction.RefundedAmount + command.Amount > transaction.Amount)
        {
            return Result<RecordPaymentRefundResponse>.BadRequest("Refund amount exceeds the remaining transaction amount.");
        }

        var refundReference = command.PayFastReference;
        if (command.ProcessWithPayFast)
        {
            if (transaction.ProviderPaymentId is null)
            {
                return Result<RecordPaymentRefundResponse>.BadRequest("This transaction has no provider payment id. Record a manual refund instead.");
            }

            var providerRefund = await payFastClient.RefundPaymentAsync(transaction.ProviderPaymentId, command.Amount, command.Reason, cancellationToken);
            if (!providerRefund.Succeeded)
            {
                return Result<RecordPaymentRefundResponse>.BadRequest(providerRefund.ErrorMessage ?? "PayFast refund failed. Record a manual refund instead.");
            }

            refundReference = providerRefund.Reference ?? refundReference;
        }

        subscription.TryRecordRefund(command.TransactionId, command.Amount, command.Reason, command.CreditNoteUrl, refundReference);
        var updatedTransaction = subscription.PaymentTransactions.Single(t => t.Id == command.TransactionId);

        events.CollectEvent(new PaymentRefunded(
                subscription.Id,
                subscription.Plan,
                subscription.PaymentTransactions.Count(t => t.RefundedAmount > 0),
                SubscriptionPlanPricing.GetMonthlyPrice(subscription.Plan),
                SubscriptionPlanPricing.Currency
            )
        );

        subscriptionRepository.Update(subscription);
        return new RecordPaymentRefundResponse(updatedTransaction.Id, updatedTransaction.RefundedAmount, updatedTransaction.Status, updatedTransaction.RefundReference);
    }
}

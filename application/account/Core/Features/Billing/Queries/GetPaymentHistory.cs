using Account.Features.Subscriptions.Domain;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Account.Features.Billing.Queries;

[PublicAPI]
public sealed record GetPaymentHistoryQuery(int PageOffset = 0, int PageSize = 10) : IRequest<Result<PaymentHistoryResponse>>;

[PublicAPI]
public sealed record PaymentHistoryResponse(int TotalCount, PaymentTransactionResponse[] Transactions);

[PublicAPI]
public sealed record PaymentTransactionResponse(
    PaymentTransactionId Id,
    decimal Amount,
    string Currency,
    PaymentTransactionStatus Status,
    DateTimeOffset Date,
    string? InvoiceUrl,
    string? CreditNoteUrl,
    string? Provider,
    string? ProviderPaymentId,
    string? ProviderStatus,
    decimal RefundedAmount,
    string RefundStatus
);

public sealed class GetPaymentHistoryHandler(ISubscriptionRepository subscriptionRepository, IExecutionContext executionContext)
    : IRequestHandler<GetPaymentHistoryQuery, Result<PaymentHistoryResponse>>
{
    public async Task<Result<PaymentHistoryResponse>> Handle(GetPaymentHistoryQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result<PaymentHistoryResponse>.Forbidden("Only owners can view payment history.");
        }

        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        var allTransactions = subscription.PaymentTransactions
            .OrderByDescending(t => t.Date)
            .ToArray();

        var paginatedTransactions = allTransactions
            .Skip(query.PageOffset * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new PaymentTransactionResponse(
                    t.Id,
                    t.Amount,
                    t.Currency,
                    t.Status,
                    t.Date,
                    t.InvoiceUrl,
                    t.CreditNoteUrl,
                    t.Provider,
                    t.ProviderPaymentId,
                    t.ProviderStatus,
                    t.RefundedAmount,
                    GetRefundStatus(t)
                )
            )
            .ToArray();

        return new PaymentHistoryResponse(allTransactions.Length, paginatedTransactions);
    }

    private static string GetRefundStatus(PaymentTransaction transaction)
    {
        if (transaction.RefundedAmount <= 0) return "None";
        return transaction.RefundedAmount >= transaction.Amount ? "Refunded" : "PartiallyRefunded";
    }
}

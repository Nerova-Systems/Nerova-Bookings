using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Jobs;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Billing.Queries;

[PublicAPI]
public sealed record GetBackOfficeBillingTenantQuery(TenantId TenantId) : IRequest<Result<BackOfficeBillingTenantResponse>>;

[PublicAPI]
public sealed record BackOfficeBillingTenantResponse(
    TenantId TenantId,
    TenantState TenantState,
    SuspensionReason? SuspensionReason,
    SubscriptionStatus SubscriptionStatus,
    SubscriptionPlan Plan,
    string? PayFastToken,
    DateTimeOffset? FirstPaymentFailedAt,
    DateTimeOffset? GracePeriodEndsAt,
    PaymentTransactionResponse[] Transactions,
    PayFastItnEventResponse[] RecentItnEvents,
    BillingReconciliationRunResponse[] ReconciliationRuns
);

[PublicAPI]
public sealed record PayFastItnEventResponse(
    string PfPaymentId,
    string PaymentStatus,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt
);

[PublicAPI]
public sealed record BillingReconciliationRunResponse(
    Guid Id,
    TenantId TenantId,
    BillingReconciliationStatus Status,
    string Summary,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt
);

public sealed class GetBackOfficeBillingTenantHandler(
    ISubscriptionRepository subscriptionRepository,
    ITenantRepository tenantRepository,
    AccountDbContext dbContext
) : IRequestHandler<GetBackOfficeBillingTenantQuery, Result<BackOfficeBillingTenantResponse>>
{
    public async Task<Result<BackOfficeBillingTenantResponse>> Handle(GetBackOfficeBillingTenantQuery query, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(query.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result<BackOfficeBillingTenantResponse>.NotFound("Tenant was not found.");
        }

        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(query.TenantId, cancellationToken);
        if (subscription is null)
        {
            return Result<BackOfficeBillingTenantResponse>.NotFound("Subscription was not found.");
        }

        var recentEvents = await dbContext.Set<PayFastItnEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == query.TenantId)
            .OrderByDescending(e => e.ReceivedAt)
            .Take(20)
            .Select(e => new PayFastItnEventResponse(e.PfPaymentId, e.PaymentStatus, e.ReceivedAt, e.ProcessedAt))
            .ToArrayAsync(cancellationToken);

        var reconciliationRuns = await dbContext.Set<BillingReconciliationRun>()
            .Where(e => e.TenantId == query.TenantId)
            .OrderByDescending(e => e.StartedAt)
            .Take(20)
            .Select(e => new BillingReconciliationRunResponse(e.Id, e.TenantId, e.Status, e.Summary, e.StartedAt, e.CompletedAt))
            .ToArrayAsync(cancellationToken);

        var transactions = subscription.PaymentTransactions
            .OrderByDescending(t => t.Date)
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
                    t.RefundedAmount <= 0 ? "None" : t.RefundedAmount >= t.Amount ? "Refunded" : "PartiallyRefunded"
                )
            )
            .ToArray();

        var gracePeriodEndsAt = subscription.FirstPaymentFailedAt?.Add(BillingDunningService.PaymentGracePeriod);
        return new BackOfficeBillingTenantResponse(
            query.TenantId,
            tenant.State,
            tenant.SuspensionReason,
            subscription.Status,
            subscription.Plan,
            subscription.PayFastToken,
            subscription.FirstPaymentFailedAt,
            gracePeriodEndsAt,
            transactions,
            recentEvents,
            reconciliationRuns
        );
    }
}

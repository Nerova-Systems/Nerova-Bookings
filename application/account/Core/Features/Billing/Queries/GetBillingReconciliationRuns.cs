using Account.Database;
using Account.Features.Subscriptions.Domain;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;

namespace Account.Features.Billing.Queries;

[PublicAPI]
public sealed record GetBillingReconciliationRunsQuery(BillingReconciliationStatus? Status = null, int PageOffset = 0, int PageSize = 50)
    : IRequest<Result<BillingReconciliationRunsResponse>>;

[PublicAPI]
public sealed record BillingReconciliationRunsResponse(int TotalCount, BillingReconciliationRunResponse[] Runs);

public sealed class GetBillingReconciliationRunsHandler(AccountDbContext dbContext)
    : IRequestHandler<GetBillingReconciliationRunsQuery, Result<BillingReconciliationRunsResponse>>
{
    public async Task<Result<BillingReconciliationRunsResponse>> Handle(GetBillingReconciliationRunsQuery query, CancellationToken cancellationToken)
    {
        var runsQuery = dbContext.Set<BillingReconciliationRun>().AsQueryable();
        if (query.Status is not null)
        {
            runsQuery = runsQuery.Where(r => r.Status == query.Status);
        }

        var totalCount = await runsQuery.CountAsync(cancellationToken);
        var runs = await runsQuery
            .OrderByDescending(r => r.StartedAt)
            .Skip(query.PageOffset * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new BillingReconciliationRunResponse(r.Id, r.TenantId, r.Status, r.Summary, r.StartedAt, r.CompletedAt))
            .ToArrayAsync(cancellationToken);

        return new BillingReconciliationRunsResponse(totalCount, runs);
    }
}

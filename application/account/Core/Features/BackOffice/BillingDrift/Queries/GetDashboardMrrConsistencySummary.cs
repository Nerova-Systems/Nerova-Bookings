using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Integrations.Paystack;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace Account.Features.BackOffice.BillingDrift.Queries;

[PublicAPI]
public sealed record GetDashboardMrrConsistencySummaryQuery : IRequest<Result<DashboardMrrConsistencySummaryResponse>>;

[PublicAPI]
public sealed record DashboardMrrConsistencySummaryResponse(decimal KpiMonthlyRecurringRevenue, decimal TrendLatestMonthlyRecurringRevenue, string? Currency);

public sealed class GetDashboardMrrConsistencySummaryHandler(ISubscriptionRepository subscriptionRepository, IBillingEventRepository billingEventRepository, IPlatformCurrencyProvider platformCurrencyProvider)
    : IRequestHandler<GetDashboardMrrConsistencySummaryQuery, Result<DashboardMrrConsistencySummaryResponse>>
{
    // Soft-delete semantic: both sides of the consistency comparison include every active subscription / MRR-changing
    // billing event regardless of tenant soft-delete state. Subscription and BillingEvent rows are immutable historical
    // money facts; the comparison only stays meaningful if both sides share the same retention rule.

    // Sub-cent diffs between KPI and trend-latest MRR are accounting noise, not drift. The FE banner
    // does strict equality so we snap trend-latest to KPI when the absolute diff is below tolerance.
    private const decimal ToleranceAmount = 0.01m;

    public async Task<Result<DashboardMrrConsistencySummaryResponse>> Handle(GetDashboardMrrConsistencySummaryQuery query, CancellationToken cancellationToken)
    {
        var paidSubscriptions = await subscriptionRepository.GetAllActiveUnfilteredAsync(cancellationToken);
        var kpiMrr = paidSubscriptions.Sum(MrrCalculator.ForwardMrr);

        // Trend-latest MRR mirrors the current dashboard trend fallback: per subscription, take the latest
        // event's NewAmount, but Paystack-owned subscriptions fall back to their live snapshot when the
        // locally-owned subscription state has moved forward and the event ledger is still catching up.
        var events = await billingEventRepository.GetMrrChangeEventsUnfilteredAsync(cancellationToken);
        var paidSubscriptionsById = paidSubscriptions.ToDictionary(s => s.Id);
        var eventSubscriptionIds = events.Select(e => e.SubscriptionId).ToHashSet();
        var latestEventMrr = events
            .GroupBy(e => e.SubscriptionId)
            .Sum(g =>
                {
                    var latestEventMrr = g.OrderByDescending(e => e.OccurredAt).First().NewAmount ?? 0m;
                    if (!paidSubscriptionsById.TryGetValue(g.Key, out var subscription)) return latestEventMrr;

                    var liveMrr = MrrCalculator.ForwardMrr(subscription);
                    return ShouldUsePaystackLiveSnapshot(subscription, liveMrr, latestEventMrr)
                        ? liveMrr
                        : latestEventMrr;
                }
            );
        var eventlessSnapshotMrr = paidSubscriptions
            .Where(s => !eventSubscriptionIds.Contains(s.Id))
            .Sum(MrrCalculator.ForwardMrr);
        var trendLatestMrr = latestEventMrr + eventlessSnapshotMrr;

        if (Math.Abs(kpiMrr - trendLatestMrr) < ToleranceAmount)
        {
            trendLatestMrr = kpiMrr;
        }

        return new DashboardMrrConsistencySummaryResponse(kpiMrr, trendLatestMrr, platformCurrencyProvider.Currency);
    }

    private static bool ShouldUsePaystackLiveSnapshot(Subscription subscription, decimal liveMrr, decimal latestEventMrr)
    {
        return subscription.PaystackCustomerId is not null
               && !subscription.CancelAtPeriodEnd
               && liveMrr > latestEventMrr;
    }
}

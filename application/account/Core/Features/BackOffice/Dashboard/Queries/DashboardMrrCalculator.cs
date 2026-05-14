using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;

namespace Account.Features.BackOffice.Dashboard.Queries;

/// <summary>
///     Reconstructs MRR on a given date from the <see cref="BillingEvent" /> log, with a live-subscription
///     fallback for Paystack subscriptions that have no MRR event rows yet. Shared between
///     <see cref="GetDashboardMrrTrendHandler" /> and <see cref="GetDashboardKpisHandler" />.
/// </summary>
internal static class DashboardMrrCalculator
{
    public static decimal ComputeMrrOnDate(Dictionary<SubscriptionId, BillingEvent[]> eventsBySubscription, DateOnly date)
    {
        var endOfDay = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var total = 0m;
        foreach (var subscriptionEvents in eventsBySubscription.Values)
        {
            var latest = subscriptionEvents.LastOrDefault(e => e.OccurredAt < endOfDay);
            if (latest?.NewAmount is { } amount) total += amount;
        }

        return total;
    }

    public static decimal ComputeMrrOnDate(Dictionary<SubscriptionId, BillingEvent[]> eventsBySubscription, Subscription[] activeSubscriptions, DateOnly date)
    {
        var total = ComputeMrrOnDate(eventsBySubscription, date);
        foreach (var subscription in activeSubscriptions)
        {
            if (eventsBySubscription.ContainsKey(subscription.Id)) continue;
            if (GetSnapshotEffectiveDate(subscription) > date) continue;

            total += MrrCalculator.ForwardMrr(subscription);
        }

        return total;
    }

    public static Dictionary<SubscriptionId, BillingEvent[]> GroupByOccurredAt(BillingEvent[] events)
    {
        return events
            .GroupBy(e => e.SubscriptionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.OccurredAt).ToArray());
    }

    private static DateOnly GetSnapshotEffectiveDate(Subscription subscription)
    {
        var firstPaidTransactionDate = subscription.PaymentTransactions
            .Where(t => t.Status is PaymentTransactionStatus.Succeeded or PaymentTransactionStatus.Refunded)
            .OrderBy(t => t.Date)
            .Select(t => (DateTimeOffset?)t.Date)
            .FirstOrDefault();

        var effectiveAt = subscription.SubscribedSince
                          ?? firstPaidTransactionDate
                          ?? subscription.CurrentPeriodStart
                          ?? subscription.CreatedAt;

        return DateOnly.FromDateTime(effectiveAt.UtcDateTime);
    }
}

using Account.Features.Subscriptions.Domain;

namespace Account.Features.Subscriptions.Shared;

public static class SubscriptionPlanPricing
{
    public const string Currency = "ZAR";

    // Monthly prices in South African Rand
    private static readonly Dictionary<SubscriptionPlan, decimal> Prices = new()
    {
        { SubscriptionPlan.Starter, 149.00m },
        { SubscriptionPlan.Standard, 299.00m },
        { SubscriptionPlan.Premium, 599.00m }
    };

    public static decimal GetMonthlyPrice(SubscriptionPlan plan) => Prices[plan];
}

namespace Account.Features.Subscriptions.Shared;

public static class SubscriptionBillingCalculator
{
    public static decimal CalculateProratedUpgradeAmount(decimal currentAmount, decimal newAmount, DateTimeOffset? currentPeriodStart, DateTimeOffset? currentPeriodEnd, DateTimeOffset now)
    {
        var amountDifference = newAmount - currentAmount;
        if (amountDifference <= 0m)
        {
            return 0m;
        }

        if (currentPeriodStart is null || currentPeriodEnd is null || currentPeriodEnd <= currentPeriodStart || now >= currentPeriodEnd)
        {
            return decimal.Round(amountDifference, 2, MidpointRounding.AwayFromZero);
        }

        if (now <= currentPeriodStart)
        {
            return decimal.Round(amountDifference, 2, MidpointRounding.AwayFromZero);
        }

        var totalTicks = currentPeriodEnd.Value.UtcTicks - currentPeriodStart.Value.UtcTicks;
        var remainingTicks = currentPeriodEnd.Value.UtcTicks - now.UtcTicks;
        var remainingRatio = (decimal)remainingTicks / totalTicks;
        return decimal.Round(amountDifference * remainingRatio, 2, MidpointRounding.AwayFromZero);
    }
}

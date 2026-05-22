namespace Main.Features.Insights.Shared;

/// <summary>
///     Closed-open date range [From, To) used across all insights queries.
/// </summary>
public sealed record InsightsDateRange(DateTimeOffset From, DateTimeOffset To)
{
    /// <summary>Returns the prior period of equal length immediately before <see cref="From" />.</summary>
    public InsightsDateRange PriorPeriod()
    {
        var span = To - From;
        return new InsightsDateRange(From - span, From);
    }
}

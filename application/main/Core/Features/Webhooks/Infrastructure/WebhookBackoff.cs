namespace Main.Features.Webhooks.Infrastructure;

/// <summary>
///     Exponential retry schedule for webhook deliveries. Six attempts total; after the sixth
///     failure the delivery is dead-lettered. Delays are measured from the previous attempt.
///     <para>Attempt 1 → 1m → Attempt 2 → 5m → Attempt 3 → 30m → Attempt 4 → 1h → Attempt 5 → 6h → Attempt 6 → DeadLetter</para>
/// </summary>
public static class WebhookBackoff
{
    public const int MaxAttempts = 6;

    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(24)
    ];

    /// <summary>
    ///     Returns the delay to wait before the NEXT attempt after <paramref name="attemptCount" />
    ///     attempts have been made. Returns <c>null</c> when the maximum has been reached and the
    ///     delivery should be dead-lettered instead of retried.
    /// </summary>
    public static TimeSpan? GetDelayAfterAttempt(int attemptCount)
    {
        if (attemptCount < 1) throw new ArgumentOutOfRangeException(nameof(attemptCount), "Attempt count must be >= 1.");
        if (attemptCount >= MaxAttempts) return null;

        return Delays[attemptCount - 1];
    }
}

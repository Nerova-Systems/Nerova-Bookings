namespace Account.Workers;

/// <summary>
///     Placeholder for provider-side drift checks. Paystack billing is app-owned in this branch, so there
///     is no provider event-list reconciliation pass equivalent to the pulled Stripe implementation.
/// </summary>
public sealed class BillingDriftWorker(ILogger<BillingDriftWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Billing drift worker skipped: Paystack billing is reconciled through app-owned payment attempts.");
        return Task.CompletedTask;
    }
}

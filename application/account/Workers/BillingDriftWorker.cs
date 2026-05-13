using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;

namespace Account.Workers;

/// <summary>
///     Single-pass drift tripwire. Container Apps can scale workers to zero, so this intentionally checks stale
///     subscriptions once per process start instead of waiting on a periodic timer.
/// </summary>
public sealed class BillingDriftWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<BillingDriftWorker> logger
) : BackgroundService
{
    private const int MaxDegreeOfParallelism = 8;
    private static readonly TimeSpan DefaultStaleness = TimeSpan.FromHours(23);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var staleness = configuration.GetValue("BillingDrift:Staleness", DefaultStaleness);
        var cutoff = timeProvider.GetUtcNow().Subtract(staleness);

        using var scope = serviceProvider.CreateScope();
        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var subscriptions = await subscriptionRepository.GetSubscriptionsDueForDriftCheckUnfilteredAsync(cutoff, cancellationToken);
        if (subscriptions.Length == 0)
        {
            logger.LogInformation("Billing drift worker found no stale Paystack subscriptions");
            return;
        }

        var processed = 0;
        var skipped = 0;
        var failed = 0;
        await Parallel.ForEachAsync(
            subscriptions,
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = MaxDegreeOfParallelism },
            async (subscription, loopCancellationToken) =>
            {
                if (subscription.PaystackCustomerId is null)
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                try
                {
                    using var iterationCancellationTokenSource = BillingDriftIterationTimeout.CreateLinkedTokenSource(loopCancellationToken);
                    using var iterationScope = serviceProvider.CreateScope();
                    var processPendingPaystackEvents = iterationScope.ServiceProvider.GetRequiredService<ProcessPendingPaystackEvents>();
                    var detected = await processPendingPaystackEvents.DetectAsync(subscription.PaystackCustomerId, iterationCancellationTokenSource.Token);
                    if (detected)
                    {
                        Interlocked.Increment(ref processed);
                    }
                    else
                    {
                        Interlocked.Increment(ref skipped);
                    }
                }
                catch (OperationCanceledException) when (!loopCancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref failed);
                    logger.LogWarning("Billing drift check timed out for subscription '{SubscriptionId}'", subscription.Id);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    logger.LogWarning(ex, "Billing drift check failed for subscription '{SubscriptionId}'", subscription.Id);
                }
            }
        );

        logger.LogInformation("Billing drift worker completed Paystack pass: processed={Processed}, skipped={Skipped}, failed={Failed}", processed, skipped, failed);
    }
}

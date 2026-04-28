using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Account.Features.Subscriptions.Jobs;

/// <summary>
///     Runs daily at 03:00 UTC. Suspends tenants whose payment grace period has expired.
/// </summary>
public sealed class BillingDunningJob(IServiceScopeFactory scopeFactory, ILogger<BillingDunningJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntilNextRunAsync(hour: 3, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            logger.LogInformation("BillingDunningJob starting");
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<BillingDunningService>();
            await service.ProcessPastDueSubscriptionsAsync(stoppingToken);
        }
    }

    private static async Task WaitUntilNextRunAsync(int hour, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);

        try
        {
            await Task.Delay(nextRun - now, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }
}

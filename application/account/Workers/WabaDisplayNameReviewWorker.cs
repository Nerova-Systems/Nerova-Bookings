using Account.Features.WhatsApp.Infrastructure;

namespace Account.Workers;

/// <summary>
///     Runs <see cref="WabaDisplayNameReviewPoller" /> every 6 hours. Cadence matches the other
///     Meta-facing pollers in the account SCS (the drift detector is daily; this poller is more
///     frequent because Meta's review takes 1–3 business days and customers want timely banners).
/// </summary>
public sealed class WabaDisplayNameReviewWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<WabaDisplayNameReviewWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var poller = scope.ServiceProvider.GetRequiredService<WabaDisplayNameReviewPoller>();
            await poller.PollAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WabaDisplayNameReviewWorker pass failed");
        }
    }
}

using Account.Features.WhatsApp.Infrastructure;

namespace Account.Workers;

/// <summary>
///     Runs <see cref="WabaProfileDriftDetector" /> once every 24 hours to catch silent drift
///     between Meta's <c>whatsapp_business_profile</c> and the tenant's local
///     <c>BrandProfile</c>. Drift is enqueued back through the regular outbox so the sync worker
///     reconciles it on its next pass.
/// </summary>
public sealed class WabaProfileDriftWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<WabaProfileDriftWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan DriftInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(DriftInterval);
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
            var detector = scope.ServiceProvider.GetRequiredService<WabaProfileDriftDetector>();
            await detector.DetectAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WabaProfileDriftWorker pass failed");
        }
    }
}

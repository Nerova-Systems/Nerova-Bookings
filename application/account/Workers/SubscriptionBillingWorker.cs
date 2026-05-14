using Account.Features.Subscriptions.Shared;

namespace Account.Workers;

public sealed class SubscriptionBillingWorker(IServiceScopeFactory serviceScopeFactory, ILogger<SubscriptionBillingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan BillingInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessBillingAsync(stoppingToken);

        using var timer = new PeriodicTimer(BillingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessBillingAsync(stoppingToken);
        }
    }

    private async Task ProcessBillingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessSubscriptionBilling>();
            await processor.ExecuteAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription billing processing failed");
        }
    }
}

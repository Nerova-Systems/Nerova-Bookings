using Account.Features.Billing.Commands;
using Account.Features.Subscriptions.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Account.Features.Subscriptions.Jobs;

public sealed class BillingReconciliationJob(IServiceScopeFactory scopeFactory, ILogger<BillingReconciliationJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntilNextRunAsync(hour: 4, stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            logger.LogInformation("BillingReconciliationJob starting");
            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var subscriptions = await subscriptionRepository.GetAllForReconciliationUnfilteredAsync(cancellationToken);
        logger.LogInformation("BillingReconciliationJob found {Count} subscriptions to reconcile", subscriptions.Length);

        foreach (var subscription in subscriptions)
        {
            var result = await mediator.Send(new ReconcileTenantBillingCommand(subscription.TenantId), cancellationToken);
            if (!result.IsSuccess)
            {
                logger.LogWarning("Billing reconciliation failed for tenant {TenantId}: {Error}", subscription.TenantId, result.ErrorMessage?.Message);
            }
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
        }
    }
}

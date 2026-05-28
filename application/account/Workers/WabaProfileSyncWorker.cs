using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Infrastructure;

namespace Account.Workers;

/// <summary>
///     Polls the <c>waba_profile_sync_outbox</c> every 30 seconds, picks up to
///     <see cref="BatchSize" /> due rows, and dispatches each to a freshly-scoped
///     <see cref="WabaProfileSyncProcessor" />. Per-row scopes provide failure isolation: a row
///     that throws cannot leave the next row's <c>AccountDbContext</c> in a poisoned state.
/// </summary>
public sealed class WabaProfileSyncWorker(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<WabaProfileSyncWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessBatchAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessBatchAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            WabaProfileSyncOutboxId[] dueIds;
            using (var outerScope = serviceScopeFactory.CreateScope())
            {
                var repository = outerScope.ServiceProvider.GetRequiredService<IWabaProfileSyncOutboxRepository>();
                var due = await repository.GetBatchDueAsync(timeProvider.GetUtcNow(), BatchSize, stoppingToken);
                dueIds = due.Select(d => d.Id).ToArray();
            }

            if (dueIds.Length == 0) return;

            foreach (var id in dueIds)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    using var innerScope = serviceScopeFactory.CreateScope();
                    var processor = innerScope.ServiceProvider.GetRequiredService<WabaProfileSyncProcessor>();
                    await processor.ProcessAsync(id, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    // Processor catches its own exceptions; this is a defensive boundary against
                    // infrastructure failures (DI resolution, scope disposal, etc.) so one row
                    // never takes down the worker.
                    logger.LogError(ex, "WabaProfileSyncWorker failed to process outbox row '{Id}'", id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WabaProfileSyncWorker batch pass failed");
        }
    }
}

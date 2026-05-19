using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Main.Features.BookingSideEffects.Workers;

public sealed class BookingSideEffectWorker(IServiceScopeFactory serviceScopeFactory, ILogger<BookingSideEffectWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<BookingSideEffectProcessor>();
                await processor.ProcessPendingAsync(50, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Booking side-effect worker failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}

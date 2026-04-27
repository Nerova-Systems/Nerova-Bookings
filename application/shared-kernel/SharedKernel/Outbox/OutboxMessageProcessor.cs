using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SharedKernel.Outbox;

public sealed class OutboxMessageProcessor<TContext>(IServiceScopeFactory scopeFactory, ILogger<OutboxMessageProcessor<TContext>> logger)
    : BackgroundService where TContext : DbContext
{
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessDueMessagesAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task ProcessDueMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>().ToDictionary(h => h.MessageType);
        var now = timeProvider.GetUtcNow();

        var messages = await dbContext.Set<OutboxMessage>()
            .AsTracking()
            .Where(m => m.ProcessedAt == null && m.DeadLetteredAt == null)
            .ToArrayAsync(cancellationToken);

        messages = messages
            .OrderBy(m => m.CreatedAt)
            .Where(m => m.NextAttemptAt <= now)
            .Where(m => m.LockedUntilAt == null || m.LockedUntilAt < now)
            .Take(BatchSize)
            .ToArray();

        foreach (var message in messages)
        {
            message.Lock(now.AddMinutes(5));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var message in messages)
        {
            await ProcessMessageAsync(message, handlers, dbContext, timeProvider, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(OutboxMessage message, IReadOnlyDictionary<string, IOutboxMessageHandler> handlers, DbContext dbContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        try
        {
            if (!handlers.TryGetValue(message.Type, out var handler))
            {
                throw new InvalidOperationException($"No outbox message handler is registered for message type '{message.Type}'.");
            }

            await handler.HandleAsync(message, cancellationToken);
            message.MarkProcessed(now);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to process outbox message {OutboxMessageId} of type {OutboxMessageType}", message.Id, message.Type);
            if (message.Attempts + 1 >= OutboxMessage.MaximumAttempts)
            {
                message.MarkDeadLettered(ex.Message, now);
            }
            else
            {
                message.MarkFailed(ex.Message, now.AddSeconds(GetRetryDelaySeconds(message.Attempts)));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static int GetRetryDelaySeconds(int attempts)
    {
        return Math.Min(300, (int)Math.Pow(2, Math.Min(attempts, 8)));
    }
}

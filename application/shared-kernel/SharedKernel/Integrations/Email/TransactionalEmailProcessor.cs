using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Integrations.Email;

public sealed class TransactionalEmailProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    ILogger<TransactionalEmailProcessor<TContext>> logger
) where TContext : DbContext
{
    public async Task ProcessDueMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var emailClient = scope.ServiceProvider.GetRequiredService<IEmailClient>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        var messages = (await dbContext.Set<TransactionalEmailMessage>()
            .AsTracking()
            .Where(e => e.Status == TransactionalEmailStatus.Pending)
            .Take(100)
            .ToArrayAsync(cancellationToken))
            .Where(e => e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .ThenBy(e => e.CreatedAt)
            .Take(25)
            .ToArray();

        foreach (var message in messages)
        {
            try
            {
                await emailClient.SendAsync(message.Recipient, message.Subject, message.HtmlContent, cancellationToken);
                message.MarkSent(timeProvider.GetUtcNow());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to send transactional email {MessageId} to {Recipient}", message.Id, message.Recipient);
                message.MarkFailed(ex.Message, timeProvider.GetUtcNow());
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

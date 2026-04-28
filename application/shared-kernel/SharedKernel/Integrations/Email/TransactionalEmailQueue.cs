using Microsoft.EntityFrameworkCore;

namespace SharedKernel.Integrations.Email;

public sealed class TransactionalEmailQueue<TContext>(TContext dbContext, TimeProvider timeProvider) : ITransactionalEmailQueue
    where TContext : DbContext
{
    public async Task EnqueueAsync(
        string recipient,
        string subject,
        string htmlContent,
        string templateKey,
        string? correlationId,
        CancellationToken cancellationToken
    )
    {
        var message = TransactionalEmailMessage.Create(recipient, subject, htmlContent, templateKey, correlationId, timeProvider.GetUtcNow());
        await dbContext.Set<TransactionalEmailMessage>().AddAsync(message, cancellationToken);
    }
}

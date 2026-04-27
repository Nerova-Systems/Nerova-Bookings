using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Configuration;

namespace SharedKernel.Outbox;

public sealed class OutboxPublisher(DbContext dbContext, TimeProvider timeProvider) : IOutboxPublisher
{
    public async Task<OutboxMessage> EnqueueAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var messageType = typeof(TMessage).FullName ?? throw new InvalidOperationException($"Message '{typeof(TMessage).Name}' must have a full type name.");
        var payload = JsonSerializer.Serialize(message, SharedDependencyConfiguration.DefaultJsonSerializerOptions);
        var outboxMessage = OutboxMessage.Create(messageType, payload, timeProvider.GetUtcNow());

        await dbContext.Set<OutboxMessage>().AddAsync(outboxMessage, cancellationToken);

        return outboxMessage;
    }
}

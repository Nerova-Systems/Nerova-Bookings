namespace SharedKernel.Outbox;

public interface IOutboxPublisher
{
    Task<OutboxMessage> EnqueueAsync<TMessage>(TMessage message, CancellationToken cancellationToken);
}

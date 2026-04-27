namespace SharedKernel.Outbox;

public interface IOutboxMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}

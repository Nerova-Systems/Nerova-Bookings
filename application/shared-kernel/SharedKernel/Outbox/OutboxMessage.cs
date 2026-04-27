namespace SharedKernel.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(Guid id, string type, string payload, DateTimeOffset now)
    {
        Id = id;
        Type = type;
        Payload = payload;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? LockedUntilAt { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(string type, string payload, DateTimeOffset now)
    {
        return new OutboxMessage(Guid.NewGuid(), type, payload, now);
    }

    public void Lock(DateTimeOffset lockedUntilAt)
    {
        LockedUntilAt = lockedUntilAt;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        LockedUntilAt = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset nextAttemptAt)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        NextAttemptAt = nextAttemptAt;
        LockedUntilAt = null;
    }
}

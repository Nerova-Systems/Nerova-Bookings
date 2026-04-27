namespace SharedKernel.Outbox;

public sealed class OutboxMessage
{
    public const int MaximumAttempts = 10;

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

    public DateTimeOffset? DeadLetteredAt { get; private set; }

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
        if (DeadLetteredAt is not null)
        {
            return;
        }

        LockedUntilAt = lockedUntilAt;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        DeadLetteredAt = null;
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

    public void MarkDeadLettered(string error, DateTimeOffset deadLetteredAt)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        DeadLetteredAt = deadLetteredAt;
        NextAttemptAt = deadLetteredAt;
        LockedUntilAt = null;
    }

    public void Retry(DateTimeOffset nextAttemptAt)
    {
        if (ProcessedAt is not null)
        {
            throw new InvalidOperationException("Processed outbox messages cannot be retried.");
        }

        DeadLetteredAt = null;
        LockedUntilAt = null;
        NextAttemptAt = nextAttemptAt;
    }

    public OutboxMessageStatus GetStatus(DateTimeOffset now)
    {
        if (ProcessedAt is not null) return OutboxMessageStatus.Processed;
        if (DeadLetteredAt is not null) return OutboxMessageStatus.DeadLettered;
        if (LockedUntilAt is not null && LockedUntilAt > now) return OutboxMessageStatus.Locked;
        if (NextAttemptAt > now) return OutboxMessageStatus.Scheduled;
        return OutboxMessageStatus.Pending;
    }
}

public enum OutboxMessageStatus
{
    Pending,
    Scheduled,
    Locked,
    Processed,
    DeadLettered
}

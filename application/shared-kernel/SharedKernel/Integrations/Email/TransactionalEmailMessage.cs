using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SharedKernel.Integrations.Email;

public sealed class TransactionalEmailMessage
{
    public const int MaximumAttempts = 5;

    private TransactionalEmailMessage()
    {
    }

    private TransactionalEmailMessage(
        string recipient,
        string subject,
        string htmlContent,
        string templateKey,
        string? correlationId,
        DateTimeOffset now
    )
    {
        Id = Guid.NewGuid();
        Recipient = recipient;
        Subject = subject;
        HtmlContent = htmlContent;
        TemplateKey = templateKey;
        CorrelationId = correlationId;
        Status = TransactionalEmailStatus.Pending;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    public Guid Id { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string HtmlContent { get; private set; } = string.Empty;
    public string TemplateKey { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public TransactionalEmailStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? DeadLetteredAt { get; private set; }
    public string? LastError { get; private set; }

    public static TransactionalEmailMessage Create(
        string recipient,
        string subject,
        string htmlContent,
        string templateKey,
        string? correlationId,
        DateTimeOffset now
    )
    {
        return new TransactionalEmailMessage(recipient.Trim().ToLowerInvariant(), subject.Trim(), htmlContent, templateKey, correlationId, now);
    }

    public void MarkSent(DateTimeOffset now)
    {
        Attempts++;
        Status = TransactionalEmailStatus.Sent;
        SentAt = now;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;

        if (Attempts >= MaximumAttempts)
        {
            Status = TransactionalEmailStatus.DeadLettered;
            DeadLetteredAt = now;
            NextAttemptAt = now;
            return;
        }

        Status = TransactionalEmailStatus.Pending;
        var delaySeconds = Math.Min(60 * Math.Pow(2, Attempts - 1), 3600);
        NextAttemptAt = now.AddSeconds(delaySeconds);
    }

    public void Retry(DateTimeOffset now)
    {
        if (Status == TransactionalEmailStatus.Sent)
        {
            return;
        }

        Status = TransactionalEmailStatus.Pending;
        DeadLetteredAt = null;
        NextAttemptAt = now;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionalEmailStatus
{
    Pending,
    Sent,
    DeadLettered
}

public sealed class TransactionalEmailMessageConfiguration : IEntityTypeConfiguration<TransactionalEmailMessage>
{
    public void Configure(EntityTypeBuilder<TransactionalEmailMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.HtmlContent).IsRequired();
        builder.Property(e => e.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(200);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });
        builder.HasIndex(e => e.TemplateKey);
        builder.HasIndex(e => e.CorrelationId);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Main.Features.Payments.Domain;

/// <summary>
///     Idempotency record — one row per Paystack webhook event we have already applied. The webhook
///     handler consults this table before mutating any booking so that re-deliveries by Paystack
///     (which retries until it gets a 2xx) are safe no-ops.
/// </summary>
public sealed class ProcessedPaymentEvent
{
    private ProcessedPaymentEvent()
    {
    }

    private ProcessedPaymentEvent(string eventId, DateTimeOffset processedAt)
    {
        EventId = eventId;
        ProcessedAt = processedAt;
    }

    public string EventId { get; private init; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private init; }

    public static ProcessedPaymentEvent Create(string eventId, DateTimeOffset processedAt)
    {
        return new ProcessedPaymentEvent(eventId, processedAt);
    }
}

public sealed class ProcessedPaymentEventConfiguration : IEntityTypeConfiguration<ProcessedPaymentEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedPaymentEvent> builder)
    {
        builder.ToTable("processed_payment_events");
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId).HasMaxLength(128);
    }
}

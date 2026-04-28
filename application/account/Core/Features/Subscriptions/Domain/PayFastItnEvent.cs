using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Subscriptions.Domain;

public sealed class PayFastItnEvent
{
    private PayFastItnEvent()
    {
    }

    private PayFastItnEvent(TenantId tenantId, string pfPaymentId, string paymentStatus, string payloadJson, DateTimeOffset receivedAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PfPaymentId = pfPaymentId;
        PaymentStatus = paymentStatus;
        EventKey = CreateEventKey(tenantId, pfPaymentId, paymentStatus);
        PayloadJson = payloadJson;
        ReceivedAt = receivedAt;
        ProcessedAt = receivedAt;
    }

    public Guid Id { get; private set; }

    public TenantId TenantId { get; private set; } = null!;

    public string PfPaymentId { get; private set; } = string.Empty;

    public string PaymentStatus { get; private set; } = string.Empty;

    public string EventKey { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public static PayFastItnEvent Create(TenantId tenantId, string pfPaymentId, string paymentStatus, string payloadJson, DateTimeOffset receivedAt)
    {
        return new PayFastItnEvent(tenantId, pfPaymentId, paymentStatus, payloadJson, receivedAt);
    }

    public static string CreateEventKey(TenantId tenantId, string pfPaymentId, string paymentStatus)
    {
        return $"{tenantId.Value}:{pfPaymentId}:{paymentStatus}".ToLowerInvariant();
    }
}

public sealed class PayFastItnEventConfiguration : IEntityTypeConfiguration<PayFastItnEvent>
{
    public void Configure(EntityTypeBuilder<PayFastItnEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.MapStronglyTypedLongId(e => e.TenantId);
        builder.Property(e => e.PfPaymentId).IsRequired();
        builder.Property(e => e.PaymentStatus).IsRequired();
        builder.Property(e => e.EventKey).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => e.EventKey).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.PfPaymentId });
    }
}

using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.Subscriptions.Domain;

public sealed class BillingReconciliationRun
{
    private BillingReconciliationRun()
    {
    }

    public Guid Id { get; private init; }

    public TenantId TenantId { get; private init; } = null!;

    public BillingReconciliationStatus Status { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private init; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static BillingReconciliationRun Start(TenantId tenantId, DateTimeOffset startedAt)
    {
        return new BillingReconciliationRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = BillingReconciliationStatus.Failed,
            Summary = "Reconciliation started.",
            StartedAt = startedAt
        };
    }

    public void Complete(BillingReconciliationStatus status, string summary, DateTimeOffset completedAt)
    {
        Status = status;
        Summary = summary;
        CompletedAt = completedAt;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingReconciliationStatus
{
    Matched,
    Corrected,
    NeedsManualReview,
    Failed
}

public sealed class BillingReconciliationRunConfiguration : IEntityTypeConfiguration<BillingReconciliationRun>
{
    public void Configure(EntityTypeBuilder<BillingReconciliationRun> builder)
    {
        builder.HasKey(e => e.Id);
        builder.MapStronglyTypedLongId(e => e.TenantId);
        builder.Property(e => e.Status).IsRequired();
        builder.Property(e => e.Summary).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.StartedAt });
        builder.HasIndex(e => e.Status);
    }
}

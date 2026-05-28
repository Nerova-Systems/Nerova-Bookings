using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.WhatsApp.Domain;

public sealed class WabaProfileSyncOutboxConfiguration : IEntityTypeConfiguration<WabaProfileSyncOutbox>
{
    public void Configure(EntityTypeBuilder<WabaProfileSyncOutbox> builder)
    {
        builder.MapStronglyTypedLongId<WabaProfileSyncOutbox, WabaProfileSyncOutboxId>(b => b.Id);
        builder.MapStronglyTypedLongId<WabaProfileSyncOutbox, TenantId>(b => b.TenantId);

        // Serialized WabaProfileDto kept verbatim in jsonb so the sync job can replay the exact
        // bytes that were generated when the command ran (the source-of-truth BrandProfile may
        // have been mutated by a later command before the job runs).
        builder.Property(x => x.RequestedPayload).HasColumnType("jsonb");

        // Polling index: jobs ask for "pending rows whose NextAttemptAt has elapsed", oldest first.
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt })
            .HasDatabaseName("ix_waba_profile_sync_outbox_status_next_attempt_at");
    }
}

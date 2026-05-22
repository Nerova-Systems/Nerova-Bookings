using Account.Features.AuditLog.Domain;
using Account.Features.Tenants.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Account.Features.AuditLog.Infrastructure;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.MapStronglyTypedUuid<AuditLogEntry, AuditLogEntryId>(e => e.Id);
        builder.MapStronglyTypedLongId<AuditLogEntry, TenantId>(e => e.TenantId);
        builder.MapStronglyTypedNullableId<AuditLogEntry, UserId, string>(e => e.ActorUserId);

        builder.Property(e => e.ActorEmail).IsRequired();
        builder.Property(e => e.Resource).IsRequired();
        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.ResourceId);
        builder.Property(e => e.Metadata);
        builder.Property(e => e.IpAddress);
        builder.Property(e => e.UserAgent);

        // Referential integrity: cascade-delete log entries when their tenant is removed.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lookup: all entries for a given tenant (supplements the global query filter index).
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_audit_log_entries_tenant_id");

        // Lookup: filter entries by actor (admin investigation use-case).
        builder.HasIndex(e => e.ActorUserId)
            .HasFilter("actor_user_id IS NOT NULL")
            .HasDatabaseName("ix_audit_log_entries_actor_user_id");

        // Lookup: time-range queries for date-bounded reports.
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_audit_log_entries_created_at");
    }
}

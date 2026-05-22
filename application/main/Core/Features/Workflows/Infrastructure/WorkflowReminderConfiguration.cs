using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Workflows.Domain;

public sealed class WorkflowReminderConfiguration : IEntityTypeConfiguration<WorkflowReminder>
{
    public void Configure(EntityTypeBuilder<WorkflowReminder> builder)
    {
        builder.MapStronglyTypedUuid<WorkflowReminder, WorkflowReminderId>(r => r.Id);
        builder.MapStronglyTypedLongId<WorkflowReminder, TenantId>(r => r.TenantId);
        builder.MapStronglyTypedUuid<WorkflowReminder, WorkflowId>(r => r.WorkflowId);
        builder.MapStronglyTypedUuid<WorkflowReminder, BookingId>(r => r.BookingId);

        builder.Property(r => r.StepId)
            .HasConversion(
                v => v != null ? v.Value : null,
                v => v != null ? new WorkflowStepId(v) : null
            );

        builder.Property(r => r.SendTo).HasMaxLength(320);
        builder.Property(r => r.EmailSubject).HasMaxLength(500);
        builder.Property(r => r.EmailBody).HasMaxLength(5000);
        builder.Property(r => r.ReferenceId).HasMaxLength(200);
        builder.Property(r => r.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(r => new { r.TenantId, r.ScheduledDate, r.Status });
        builder.HasIndex(r => r.BookingId);
    }
}

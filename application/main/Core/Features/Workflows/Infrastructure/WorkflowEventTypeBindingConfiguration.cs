using Main.Features.EventTypes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Workflows.Domain;

public sealed class WorkflowEventTypeBindingConfiguration : IEntityTypeConfiguration<WorkflowEventTypeBinding>
{
    public void Configure(EntityTypeBuilder<WorkflowEventTypeBinding> builder)
    {
        builder.MapStronglyTypedUuid<WorkflowEventTypeBinding, WorkflowEventTypeBindingId>(b => b.Id);
        builder.MapStronglyTypedLongId<WorkflowEventTypeBinding, TenantId>(b => b.TenantId);
        builder.MapStronglyTypedUuid<WorkflowEventTypeBinding, WorkflowId>(b => b.WorkflowId);
        builder.MapStronglyTypedUuid<WorkflowEventTypeBinding, EventTypeId>(b => b.EventTypeId);

        builder.HasOne<Workflow>()
            .WithMany()
            .HasForeignKey(b => b.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<EventType>()
            .WithMany()
            .HasForeignKey(b => b.EventTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.WorkflowId, b.EventTypeId }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.EventTypeId });
    }
}

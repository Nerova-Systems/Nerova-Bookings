using Main.Features.Workflows.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain;
using SharedKernel.EntityFramework;

namespace Main.Features.Workflows.Infrastructure;

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflow_definitions");

        builder.MapStronglyTypedUuid<Workflow, WorkflowId>(w => w.Id);
        builder.MapStronglyTypedLongId<Workflow, TenantId>(w => w.TenantId);
        builder.MapStronglyTypedUuid<Workflow, UserId>(w => w.OwnerUserId);

        builder.Property(w => w.Name).HasMaxLength(100);

        builder.OwnsMany(w => w.Steps, stepBuilder =>
            {
                stepBuilder.ToTable("workflow_steps");
                stepBuilder.WithOwner().HasForeignKey("workflow_id");
                stepBuilder.HasKey(s => s.Id);
                stepBuilder.MapStronglyTypedUuid<Workflow, WorkflowStep, WorkflowStepId>(s => s.Id);
                stepBuilder.Property(s => s.SendTo).HasMaxLength(320);
                stepBuilder.Property(s => s.EmailSubject).HasMaxLength(500);
                stepBuilder.Property(s => s.EmailBody).HasMaxLength(5000);
            }
        );

        builder.Navigation(w => w.Steps).HasField("_steps").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(w => new { w.TenantId, w.OwnerUserId });
    }
}

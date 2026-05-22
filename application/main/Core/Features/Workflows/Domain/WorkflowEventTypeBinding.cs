using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;

namespace Main.Features.Workflows.Domain;

public sealed class WorkflowEventTypeBinding : AggregateRoot<WorkflowEventTypeBindingId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private WorkflowEventTypeBinding() : base(WorkflowEventTypeBindingId.NewId())
    {
        WorkflowId = new WorkflowId(string.Empty);
        EventTypeId = new EventTypeId(string.Empty);
    }

    private WorkflowEventTypeBinding(TenantId tenantId, WorkflowId workflowId, EventTypeId eventTypeId)
        : base(WorkflowEventTypeBindingId.NewId())
    {
        TenantId = tenantId;
        WorkflowId = workflowId;
        EventTypeId = eventTypeId;
    }

    public WorkflowId WorkflowId { get; private set; }

    public EventTypeId EventTypeId { get; private set; }

    public TenantId TenantId { get; } = new(0);

    public static WorkflowEventTypeBinding Create(TenantId tenantId, WorkflowId workflowId, EventTypeId eventTypeId)
    {
        return new WorkflowEventTypeBinding(tenantId, workflowId, eventTypeId);
    }
}

using JetBrains.Annotations;
using Main.Features.Workflows.Domain;

namespace Main.Features.Workflows.Shared;

[PublicAPI]
public sealed record WorkflowStepRequest(
    WorkflowAction Action,
    WorkflowReminderTemplate Template,
    int? ReminderTime,
    WorkflowTimeUnit? TimeUnit,
    string? SendTo,
    string? EmailSubject,
    string? EmailBody
);

[PublicAPI]
public sealed record WorkflowStepResponse(
    string Id,
    WorkflowAction Action,
    WorkflowReminderTemplate Template,
    int? ReminderTime,
    WorkflowTimeUnit? TimeUnit,
    string? SendTo,
    string? EmailSubject,
    string? EmailBody
)
{
    public static WorkflowStepResponse From(WorkflowStep step)
    {
        return new WorkflowStepResponse(
            step.Id.Value,
            step.Action,
            step.Template,
            step.ReminderTime,
            step.TimeUnit,
            step.SendTo,
            step.EmailSubject,
            step.EmailBody
        );
    }
}

[PublicAPI]
public sealed record WorkflowResponse(
    string Id,
    string Name,
    WorkflowTrigger Trigger,
    IReadOnlyList<WorkflowStepResponse> Steps
)
{
    public static WorkflowResponse From(Workflow workflow)
    {
        return new WorkflowResponse(
            workflow.Id.Value,
            workflow.Name,
            workflow.Trigger,
            workflow.Steps.Select(WorkflowStepResponse.From).ToList()
        );
    }
}

[PublicAPI]
public sealed record WorkflowsResponse(IReadOnlyList<WorkflowResponse> Workflows)
{
    public static WorkflowsResponse From(IEnumerable<Workflow> workflows)
    {
        return new WorkflowsResponse(workflows.Select(WorkflowResponse.From).ToList());
    }
}

[PublicAPI]
public sealed record WorkflowEventTypeBindingResponse(
    string Id,
    string WorkflowId,
    string EventTypeId
)
{
    public static WorkflowEventTypeBindingResponse From(WorkflowEventTypeBinding binding)
    {
        return new WorkflowEventTypeBindingResponse(
            binding.Id.Value,
            binding.WorkflowId.Value,
            binding.EventTypeId.Value
        );
    }
}

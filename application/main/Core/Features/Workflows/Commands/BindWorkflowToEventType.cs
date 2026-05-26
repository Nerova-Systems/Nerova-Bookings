using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Workflows.Commands;

[PublicAPI]
public sealed record BindWorkflowToEventTypeCommand(
    WorkflowId WorkflowId,
    EventTypeId EventTypeId
) : ICommand, IRequest<Result<WorkflowEventTypeBindingResponse>>;

public sealed class BindWorkflowToEventTypeHandler(
    IWorkflowRepository workflowRepository,
    IEventTypeRepository eventTypeRepository,
    IWorkflowEventTypeBindingRepository bindingRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<BindWorkflowToEventTypeCommand, Result<WorkflowEventTypeBindingResponse>>
{
    public async Task<Result<WorkflowEventTypeBindingResponse>> Handle(BindWorkflowToEventTypeCommand command, CancellationToken cancellationToken)
    {
        if (!WorkflowAuthorization.CanManageWorkflows(executionContext.UserInfo))
        {
            return Result<WorkflowEventTypeBindingResponse>.Forbidden(WorkflowAuthorization.ManageWorkflowsForbiddenMessage);
        }

        if (!executionContext.UserInfo.IsFeatureFlagEnabled(WorkflowAuthorization.WorkflowsFeatureFlagKey))
        {
            return Result<WorkflowEventTypeBindingResponse>.Forbidden(WorkflowAuthorization.WorkflowsFeatureDisabledMessage);
        }

        var tenantId = executionContext.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<WorkflowEventTypeBindingResponse>.Unauthorized("Authentication is required.");
        }

        var workflow = await workflowRepository.GetByIdAsync(command.WorkflowId, cancellationToken);
        if (workflow is null || workflow.OwnerUserId != ownerUserId)
        {
            return Result<WorkflowEventTypeBindingResponse>.NotFound($"Workflow '{command.WorkflowId}' was not found.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(command.EventTypeId, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result<WorkflowEventTypeBindingResponse>.NotFound($"Event type '{command.EventTypeId}' was not found.");
        }

        var existing = await bindingRepository.GetByWorkflowAndEventTypeAsync(command.WorkflowId, command.EventTypeId, cancellationToken);
        if (existing is not null)
        {
            return Result<WorkflowEventTypeBindingResponse>.BadRequest("This workflow is already bound to the event type.");
        }

        var binding = WorkflowEventTypeBinding.Create(tenantId, command.WorkflowId, command.EventTypeId);
        await bindingRepository.AddAsync(binding, cancellationToken);
        events.CollectEvent(new WorkflowBoundToEventType(command.WorkflowId, command.EventTypeId));

        return WorkflowEventTypeBindingResponse.From(binding);
    }
}

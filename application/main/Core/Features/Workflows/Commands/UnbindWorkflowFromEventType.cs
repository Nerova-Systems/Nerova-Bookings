using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using Main.Features.Workflows.Infrastructure;

namespace Main.Features.Workflows.Commands;

[PublicAPI]
public sealed record UnbindWorkflowFromEventTypeCommand(
    WorkflowId WorkflowId,
    EventTypeId EventTypeId
) : ICommand, IRequest<Result>;

public sealed class UnbindWorkflowFromEventTypeHandler(
    IWorkflowRepository workflowRepository,
    IWorkflowEventTypeBindingRepository bindingRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UnbindWorkflowFromEventTypeCommand, Result>
{
    public async Task<Result> Handle(UnbindWorkflowFromEventTypeCommand command, CancellationToken cancellationToken)
    {
        if (!WorkflowAuthorization.CanManageWorkflows(executionContext.UserInfo))
        {
            return Result.Forbidden(WorkflowAuthorization.ManageWorkflowsForbiddenMessage);
        }

        if (!executionContext.UserInfo.IsFeatureFlagEnabled(WorkflowAuthorization.WorkflowsFeatureFlagKey))
        {
            return Result.Forbidden(WorkflowAuthorization.WorkflowsFeatureDisabledMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var workflow = await workflowRepository.GetByIdAsync(command.WorkflowId, cancellationToken);
        if (workflow is null || workflow.OwnerUserId != ownerUserId)
        {
            return Result.NotFound($"Workflow '{command.WorkflowId}' was not found.");
        }

        var binding = await bindingRepository.GetByWorkflowAndEventTypeAsync(command.WorkflowId, command.EventTypeId, cancellationToken);
        if (binding is null)
        {
            return Result.NotFound($"Workflow '{command.WorkflowId}' is not bound to event type '{command.EventTypeId}'.");
        }

        bindingRepository.Remove(binding);
        events.CollectEvent(new WorkflowUnboundFromEventType(command.WorkflowId, command.EventTypeId));

        return Result.Success();
    }
}

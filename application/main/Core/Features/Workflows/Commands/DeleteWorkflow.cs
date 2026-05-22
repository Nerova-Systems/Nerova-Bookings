using JetBrains.Annotations;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Workflows.Commands;

[PublicAPI]
public sealed record DeleteWorkflowCommand(WorkflowId WorkflowId) : ICommand, IRequest<Result>;

public sealed class DeleteWorkflowHandler(
    IWorkflowRepository workflowRepository,
    IWorkflowEventTypeBindingRepository bindingRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteWorkflowCommand, Result>
{
    public async Task<Result> Handle(DeleteWorkflowCommand command, CancellationToken cancellationToken)
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

        // Remove all bindings first
        var bindings = await bindingRepository.GetByWorkflowIdAsync(command.WorkflowId, cancellationToken);
        foreach (var binding in bindings)
        {
            bindingRepository.Remove(binding);
        }

        workflowRepository.Remove(workflow);
        events.CollectEvent(new WorkflowDeleted(workflow.Id));

        return Result.Success();
    }
}

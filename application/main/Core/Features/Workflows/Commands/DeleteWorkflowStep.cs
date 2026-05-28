using JetBrains.Annotations;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Workflows.Commands;

[PublicAPI]
public sealed record DeleteWorkflowStepCommand(
    WorkflowId WorkflowId,
    WorkflowStepId StepId
) : ICommand, IRequest<Result>;

public sealed class DeleteWorkflowStepHandler(
    IWorkflowRepository workflowRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteWorkflowStepCommand, Result>
{
    public async Task<Result> Handle(DeleteWorkflowStepCommand command, CancellationToken cancellationToken)
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

        var workflow = await workflowRepository.GetByIdWithStepsAsync(command.WorkflowId, cancellationToken);
        if (workflow is null || workflow.OwnerUserId != ownerUserId)
        {
            return Result.NotFound($"Workflow '{command.WorkflowId}' was not found.");
        }

        var step = workflow.Steps.FirstOrDefault(s => s.Id == command.StepId);
        if (step is null)
        {
            return Result.NotFound($"Step '{command.StepId}' was not found in workflow '{command.WorkflowId}'.");
        }

        workflow.RemoveStep(command.StepId);
        workflowRepository.TrackRemovedStep(step);
        events.CollectEvent(new WorkflowStepDeleted(workflow.Id, command.StepId));

        return Result.Success();
    }
}

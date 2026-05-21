using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Workflows.Commands;

[PublicAPI]
public sealed record UpdateWorkflowCommand(
    WorkflowId WorkflowId,
    string Name
) : ICommand, IRequest<Result<WorkflowResponse>>;

public sealed class UpdateWorkflowValidator : AbstractValidator<UpdateWorkflowCommand>
{
    public UpdateWorkflowValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateWorkflowHandler(
    IWorkflowRepository workflowRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateWorkflowCommand, Result<WorkflowResponse>>
{
    public async Task<Result<WorkflowResponse>> Handle(UpdateWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (!WorkflowAuthorization.CanManageWorkflows(executionContext.UserInfo))
        {
            return Result<WorkflowResponse>.Forbidden(WorkflowAuthorization.ManageWorkflowsForbiddenMessage);
        }

        if (!executionContext.UserInfo.IsFeatureFlagEnabled(WorkflowAuthorization.WorkflowsFeatureFlagKey))
        {
            return Result<WorkflowResponse>.Forbidden(WorkflowAuthorization.WorkflowsFeatureDisabledMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<WorkflowResponse>.Unauthorized("Authentication is required.");
        }

        var workflow = await workflowRepository.GetByIdAsync(command.WorkflowId, cancellationToken);
        if (workflow is null || workflow.OwnerUserId != ownerUserId)
        {
            return Result<WorkflowResponse>.NotFound($"Workflow '{command.WorkflowId}' was not found.");
        }

        if (await workflowRepository.NameExistsForOwnerAsync(ownerUserId, command.Name, command.WorkflowId, cancellationToken))
        {
            return Result<WorkflowResponse>.BadRequest($"A workflow named '{command.Name}' already exists.");
        }

        workflow.Update(command.Name);
        workflowRepository.Update(workflow);
        events.CollectEvent(new WorkflowUpdated(workflow.Id));

        return WorkflowResponse.From(workflow);
    }
}

using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Workflows.Commands;

[PublicAPI]
public sealed record CreateWorkflowCommand(
    string Name,
    WorkflowTrigger Trigger
) : ICommand, IRequest<Result<WorkflowResponse>>;

public sealed class CreateWorkflowValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateWorkflowHandler(
    IWorkflowRepository workflowRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateWorkflowCommand, Result<WorkflowResponse>>
{
    public async Task<Result<WorkflowResponse>> Handle(CreateWorkflowCommand command, CancellationToken cancellationToken)
    {
        if (!WorkflowAuthorization.CanManageWorkflows(executionContext.UserInfo))
        {
            return Result<WorkflowResponse>.Forbidden(WorkflowAuthorization.ManageWorkflowsForbiddenMessage);
        }

        if (!executionContext.UserInfo.IsFeatureFlagEnabled(WorkflowAuthorization.WorkflowsFeatureFlagKey))
        {
            return Result<WorkflowResponse>.Forbidden(WorkflowAuthorization.WorkflowsFeatureDisabledMessage);
        }

        var tenantId = executionContext.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<WorkflowResponse>.Unauthorized("Authentication is required.");
        }

        if (await workflowRepository.NameExistsForOwnerAsync(ownerUserId, command.Name, null, cancellationToken))
        {
            return Result<WorkflowResponse>.BadRequest($"A workflow named '{command.Name}' already exists.");
        }

        var workflow = Workflow.Create(tenantId, ownerUserId, command.Name, command.Trigger);
        await workflowRepository.AddAsync(workflow, cancellationToken);
        events.CollectEvent(new WorkflowCreated(workflow.Id));

        return WorkflowResponse.From(workflow);
    }
}

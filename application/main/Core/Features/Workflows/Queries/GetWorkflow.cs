using JetBrains.Annotations;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Workflows.Queries;

[PublicAPI]
public sealed record GetWorkflowQuery(WorkflowId WorkflowId) : IRequest<Result<WorkflowResponse>>;

public sealed class GetWorkflowHandler(
    IWorkflowRepository workflowRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetWorkflowQuery, Result<WorkflowResponse>>
{
    public async Task<Result<WorkflowResponse>> Handle(GetWorkflowQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(WorkflowAuthorization.WorkflowsFeatureFlagKey))
        {
            return Result<WorkflowResponse>.Forbidden(WorkflowAuthorization.WorkflowsFeatureDisabledMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<WorkflowResponse>.Unauthorized("Authentication is required.");
        }

        var workflow = await workflowRepository.GetByIdWithStepsAsync(query.WorkflowId, cancellationToken);
        if (workflow is null || workflow.OwnerUserId != ownerUserId)
        {
            return Result<WorkflowResponse>.NotFound($"Workflow '{query.WorkflowId}' was not found.");
        }

        return WorkflowResponse.From(workflow);
    }
}

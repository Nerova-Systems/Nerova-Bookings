using JetBrains.Annotations;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Workflows.Queries;

[PublicAPI]
public sealed record GetWorkflowsQuery : IRequest<Result<WorkflowsResponse>>;

public sealed class GetWorkflowsHandler(
    IWorkflowRepository workflowRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetWorkflowsQuery, Result<WorkflowsResponse>>
{
    public async Task<Result<WorkflowsResponse>> Handle(GetWorkflowsQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(WorkflowAuthorization.WorkflowsFeatureFlagKey))
        {
            return Result<WorkflowsResponse>.Forbidden(WorkflowAuthorization.WorkflowsFeatureDisabledMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<WorkflowsResponse>.Unauthorized("Authentication is required.");
        }

        var workflows = await workflowRepository.GetForOwnerAsync(ownerUserId, cancellationToken);
        return WorkflowsResponse.From(workflows);
    }
}

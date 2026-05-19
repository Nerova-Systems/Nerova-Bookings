using JetBrains.Annotations;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.BookingSideEffects.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.BookingSideEffects.Queries;

[PublicAPI]
public sealed record GetWorkflowsQuery(EventTypeId EventTypeId) : IRequest<Result<WorkflowsResponse>>;

public sealed class GetWorkflowsHandler(
    IWorkflowRepository workflowRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetWorkflowsQuery, Result<WorkflowsResponse>>
{
    public async Task<Result<WorkflowsResponse>> Handle(GetWorkflowsQuery query, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWorkflows(executionContext);
        if (!authorization.IsSuccess) return Result<WorkflowsResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, query.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<WorkflowsResponse>.From(eventType);

        var workflows = await workflowRepository.GetForEventTypeAsync(executionContext.TenantId!, executionContext.UserInfo.Id!, query.EventTypeId, cancellationToken);
        return new WorkflowsResponse(workflows.Select(WorkflowResponse.From).ToArray());
    }
}

using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.ManagedEventTypes.Queries.GetManagedEventTypeAssignmentStatus;

[PublicAPI]
public sealed record GetManagedEventTypeAssignmentStatusQuery(EventTypeId ParentId)
    : IRequest<Result<ManagedEventTypeAssignmentStatusResponse>>;

public sealed class GetManagedEventTypeAssignmentStatusHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetManagedEventTypeAssignmentStatusQuery, Result<ManagedEventTypeAssignmentStatusResponse>>
{
    public async Task<Result<ManagedEventTypeAssignmentStatusResponse>> Handle(
        GetManagedEventTypeAssignmentStatusQuery query,
        CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!ManagedEventTypeAuthorization.HasManagedEventTypesFeature(userInfo))
            return Result<ManagedEventTypeAssignmentStatusResponse>.Forbidden(ManagedEventTypeAuthorization.ManagedEventTypesFeatureDisabledMessage);

        if (!ManagedEventTypeAuthorization.CanManageManagedEventTypes(userInfo))
            return Result<ManagedEventTypeAssignmentStatusResponse>.Forbidden(ManagedEventTypeAuthorization.ManageManagedEventTypesForbiddenMessage);

        var parent = await eventTypeRepository.GetByIdAsync(query.ParentId, cancellationToken);
        if (parent is null)
            return Result<ManagedEventTypeAssignmentStatusResponse>.NotFound($"Event type '{query.ParentId}' was not found.");

        if (parent.ParentEventTypeId is not null)
            return Result<ManagedEventTypeAssignmentStatusResponse>.BadRequest("The specified event type is not a managed template.");

        var children = await eventTypeRepository.GetChildrenAsync(query.ParentId, cancellationToken);
        return ManagedEventTypeAssignmentStatusResponse.From(parent, children);
    }
}

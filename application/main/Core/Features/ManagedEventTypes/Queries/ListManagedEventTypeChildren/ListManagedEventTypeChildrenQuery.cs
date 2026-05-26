using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.ManagedEventTypes.Queries.ListManagedEventTypeChildren;

[PublicAPI]
public sealed record ListManagedEventTypeChildrenQuery(EventTypeId ParentId)
    : IRequest<Result<ManagedEventTypeChildrenResponse>>;

public sealed class ListManagedEventTypeChildrenHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListManagedEventTypeChildrenQuery, Result<ManagedEventTypeChildrenResponse>>
{
    public async Task<Result<ManagedEventTypeChildrenResponse>> Handle(
        ListManagedEventTypeChildrenQuery query,
        CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!ManagedEventTypeAuthorization.HasManagedEventTypesFeature(userInfo))
        {
            return Result<ManagedEventTypeChildrenResponse>.Forbidden(ManagedEventTypeAuthorization.ManagedEventTypesFeatureDisabledMessage);
        }

        if (!ManagedEventTypeAuthorization.CanManageManagedEventTypes(userInfo))
        {
            return Result<ManagedEventTypeChildrenResponse>.Forbidden(ManagedEventTypeAuthorization.ManageManagedEventTypesForbiddenMessage);
        }

        var parent = await eventTypeRepository.GetByIdAsync(query.ParentId, cancellationToken);
        if (parent is null)
        {
            return Result<ManagedEventTypeChildrenResponse>.NotFound($"Event type '{query.ParentId}' was not found.");
        }

        var children = await eventTypeRepository.GetChildrenAsync(query.ParentId, cancellationToken);
        return new ManagedEventTypeChildrenResponse(
            children.Select(ManagedEventTypeChildResponse.From).ToArray()
        );
    }
}

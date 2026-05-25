using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.EventTypes.Queries;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Read)]
public sealed record GetEventTypeQuery(EventTypeId Id) : IRequest<Result<EventTypeResponse>>;

public sealed class GetEventTypeHandler(IEventTypeRepository eventTypeRepository, IExecutionContext executionContext)
    : IRequestHandler<GetEventTypeQuery, Result<EventTypeResponse>>
{
    public async Task<Result<EventTypeResponse>> Handle(GetEventTypeQuery query, CancellationToken cancellationToken)
    {
        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<EventTypeResponse>.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(query.Id, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result<EventTypeResponse>.NotFound($"Event type '{query.Id}' was not found.");
        }

        return EventTypeResponse.From(eventType);
    }
}

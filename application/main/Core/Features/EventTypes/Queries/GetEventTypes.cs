using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.EventTypes.Queries;

[PublicAPI]
public sealed record GetEventTypesQuery : IRequest<Result<EventTypesResponse>>;

public sealed class GetEventTypesHandler(IEventTypeRepository eventTypeRepository, IExecutionContext executionContext)
    : IRequestHandler<GetEventTypesQuery, Result<EventTypesResponse>>
{
    public async Task<Result<EventTypesResponse>> Handle(GetEventTypesQuery query, CancellationToken cancellationToken)
    {
        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<EventTypesResponse>.Unauthorized("Authentication is required.");
        }

        var eventTypes = await eventTypeRepository.GetForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        return new EventTypesResponse(eventTypes.Select(EventTypeResponse.From).ToArray());
    }
}

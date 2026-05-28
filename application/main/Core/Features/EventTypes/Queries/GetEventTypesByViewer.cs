using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.EventTypes.Queries;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Read)]
public sealed record GetEventTypesByViewerQuery : IRequest<Result<EventTypesByViewerResponse>>;

/// <summary>
///     Returns the event types the caller can see, grouped by personal vs team.
///     Org-level grouping is intentionally omitted — see <see cref="EventTypesByViewerResponse" />.
/// </summary>
public sealed class GetEventTypesByViewerHandler(IEventTypeRepository eventTypeRepository, IExecutionContext executionContext)
    : IRequestHandler<GetEventTypesByViewerQuery, Result<EventTypesByViewerResponse>>
{
    public async Task<Result<EventTypesByViewerResponse>> Handle(GetEventTypesByViewerQuery query, CancellationToken cancellationToken)
    {
        var callerUserId = executionContext.UserInfo.Id;
        if (callerUserId is null)
        {
            return Result<EventTypesByViewerResponse>.Unauthorized("Authentication is required.");
        }

        var eventTypes = await eventTypeRepository.GetForViewerAsync(callerUserId, cancellationToken);

        var personal = eventTypes
            .Where(eventType => eventType.TeamId is null)
            .Select(EventTypeResponse.From)
            .ToArray();

        var teamGroups = eventTypes
            .Where(eventType => eventType.TeamId is not null)
            .GroupBy(eventType => eventType.TeamId!)
            .Select(group => new EventTypeGroupResponse("team", group.Key, group.Select(EventTypeResponse.From).ToArray()))
            .ToArray();

        var groups = new List<EventTypeGroupResponse> { new("personal", null, personal) };
        groups.AddRange(teamGroups);

        return new EventTypesByViewerResponse(groups.ToArray());
    }
}

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
public sealed record GetEventTypeGroupsQuery : IRequest<Result<EventTypeGroupsResponse>>;

/// <summary>
///     Returns counts for the groups the caller can see (personal + each team).
///     Org-level grouping is deferred — see <see cref="EventTypesByViewerResponse" />.
/// </summary>
public sealed class GetEventTypeGroupsHandler(IEventTypeRepository eventTypeRepository, IExecutionContext executionContext)
    : IRequestHandler<GetEventTypeGroupsQuery, Result<EventTypeGroupsResponse>>
{
    public async Task<Result<EventTypeGroupsResponse>> Handle(GetEventTypeGroupsQuery query, CancellationToken cancellationToken)
    {
        var callerUserId = executionContext.UserInfo.Id;
        if (callerUserId is null)
        {
            return Result<EventTypeGroupsResponse>.Unauthorized("Authentication is required.");
        }

        var eventTypes = await eventTypeRepository.GetForViewerAsync(callerUserId, cancellationToken);

        var personalCount = eventTypes.Count(eventType => eventType.TeamId is null);
        var teamSummaries = eventTypes
            .Where(eventType => eventType.TeamId is not null)
            .GroupBy(eventType => eventType.TeamId!)
            .Select(group => new EventTypeGroupSummaryResponse("team", group.Key, group.Count()))
            .ToArray();

        var summaries = new List<EventTypeGroupSummaryResponse> { new("personal", null, personalCount) };
        summaries.AddRange(teamSummaries);

        return new EventTypeGroupsResponse(summaries.ToArray());
    }
}

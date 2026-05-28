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
public sealed record GetHostsForAssignmentQuery(EventTypeId EventTypeId) : IRequest<Result<HostsForAssignmentResponse>>;

/// <summary>
///     Returns the candidate hosts that may be assigned to an event type. For team-scoped
///     event types this is every user who already appears as a host on any event type owned
///     by the same team; for solo event types it is just the owner. The frontend resolves
///     display names by calling <c>/api/team-members/search</c> with the returned user ids.
/// </summary>
public sealed class GetHostsForAssignmentHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetHostsForAssignmentQuery, Result<HostsForAssignmentResponse>>
{
    public async Task<Result<HostsForAssignmentResponse>> Handle(GetHostsForAssignmentQuery query, CancellationToken cancellationToken)
    {
        var callerUserId = executionContext.UserInfo.Id;
        if (callerUserId is null)
        {
            return Result<HostsForAssignmentResponse>.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(query.EventTypeId, cancellationToken);
        if (eventType is null || (eventType.TeamId is null && eventType.OwnerUserId != callerUserId))
        {
            return Result<HostsForAssignmentResponse>.NotFound($"Event type '{query.EventTypeId}' was not found.");
        }

        if (eventType.TeamId is null)
        {
            var soloCandidates = new[] { new HostCandidateResponse(eventType.OwnerUserId) };
            return new HostsForAssignmentResponse(soloCandidates);
        }

        var userIds = await hostRepository.GetDistinctUserIdsForTeamAsync(eventType.TeamId, cancellationToken);
        // Ensure the owner is always offered as a candidate, even before any hosts are saved.
        var candidates = userIds
            .Append(eventType.OwnerUserId)
            .Distinct()
            .Select(userId => new HostCandidateResponse(userId))
            .ToArray();

        return new HostsForAssignmentResponse(candidates);
    }
}

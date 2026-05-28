using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Schedules.Queries;

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Read)]
public sealed record GetScheduleQuery(ScheduleId Id) : IRequest<Result<ScheduleResponse>>;

public sealed class GetScheduleHandler(IScheduleRepository scheduleRepository, IExecutionContext executionContext)
    : IRequestHandler<GetScheduleQuery, Result<ScheduleResponse>>
{
    public async Task<Result<ScheduleResponse>> Handle(GetScheduleQuery query, CancellationToken cancellationToken)
    {
        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<ScheduleResponse>.Unauthorized("Authentication is required.");
        }

        var schedule = await scheduleRepository.GetByIdAsync(query.Id, cancellationToken);
        if (schedule is null || !ScheduleAccess.HasAccess(schedule, ownerUserId, executionContext.ActiveTeamId))
        {
            return Result<ScheduleResponse>.NotFound($"Schedule '{query.Id}' was not found.");
        }

        return ScheduleResponse.From(schedule);
    }
}

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
public sealed record GetSchedulesQuery : IRequest<Result<SchedulesResponse>>;

public sealed class GetSchedulesHandler(IScheduleRepository scheduleRepository, IExecutionContext executionContext)
    : IRequestHandler<GetSchedulesQuery, Result<SchedulesResponse>>
{
    public async Task<Result<SchedulesResponse>> Handle(GetSchedulesQuery query, CancellationToken cancellationToken)
    {
        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<SchedulesResponse>.Unauthorized("Authentication is required.");
        }

        var schedules = await scheduleRepository.GetForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        return new SchedulesResponse(schedules.Select(ScheduleResponse.From).ToArray());
    }
}

using JetBrains.Annotations;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Schedules.Queries;

[PublicAPI]
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
        if (schedule is null || schedule.OwnerUserId != ownerUserId)
        {
            return Result<ScheduleResponse>.NotFound($"Schedule '{query.Id}' was not found.");
        }

        return ScheduleResponse.From(schedule);
    }
}

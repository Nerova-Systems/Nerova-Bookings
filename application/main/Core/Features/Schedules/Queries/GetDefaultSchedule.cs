using JetBrains.Annotations;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Schedules.Queries;

[PublicAPI]
public sealed record GetDefaultScheduleQuery : IRequest<Result<ScheduleResponse>>;

public sealed class GetDefaultScheduleHandler(IScheduleRepository scheduleRepository, IExecutionContext executionContext)
    : IRequestHandler<GetDefaultScheduleQuery, Result<ScheduleResponse>>
{
    public async Task<Result<ScheduleResponse>> Handle(GetDefaultScheduleQuery query, CancellationToken cancellationToken)
    {
        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<ScheduleResponse>.Unauthorized("Authentication is required.");
        }

        var schedule = await scheduleRepository.GetDefaultForOwnerAsync(ownerUserId, cancellationToken);
        if (schedule is null)
        {
            return Result<ScheduleResponse>.NotFound("Default schedule was not found.");
        }

        return ScheduleResponse.From(schedule);
    }
}

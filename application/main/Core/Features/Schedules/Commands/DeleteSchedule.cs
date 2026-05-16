using JetBrains.Annotations;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Schedules.Commands;

[PublicAPI]
public sealed record DeleteScheduleCommand(ScheduleId Id) : ICommand, IRequest<Result>;

public sealed class DeleteScheduleHandler(
    IScheduleRepository scheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteScheduleCommand, Result>
{
    public async Task<Result> Handle(DeleteScheduleCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result.Forbidden(SchedulingAuthorization.ManageSchedulesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var schedule = await scheduleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (schedule is null || schedule.OwnerUserId != ownerUserId)
        {
            return Result.NotFound($"Schedule '{command.Id}' was not found.");
        }

        scheduleRepository.Remove(schedule);
        events.CollectEvent(new ScheduleDeleted(schedule.Id));

        return Result.Success();
    }
}

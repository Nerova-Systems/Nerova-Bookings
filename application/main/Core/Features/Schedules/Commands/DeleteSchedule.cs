using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Schedules.Commands;

[PublicAPI]
public sealed record DeleteScheduleCommand(ScheduleId Id) : ICommand, IRequest<Result>;

public sealed class DeleteScheduleHandler(
    IScheduleRepository scheduleRepository,
    IEventTypeRepository eventTypeRepository,
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
        if (schedule is null || !ScheduleAccess.HasAccess(schedule, ownerUserId, executionContext.ActiveTeamId))
        {
            return Result.NotFound($"Schedule '{command.Id}' was not found.");
        }

        var schedules = await scheduleRepository.GetForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, cancellationToken);
        if (schedules.Length == 1)
        {
            return Result.BadRequest("At least one schedule is required.");
        }

        if (schedule.IsDefault)
        {
            return Result.BadRequest("Default schedule cannot be deleted. Make another schedule default before deleting it.");
        }

        if (await eventTypeRepository.ExistsForScheduleAsync(ownerUserId, schedule.Id, cancellationToken))
        {
            return Result.BadRequest($"Schedule '{schedule.Id}' cannot be deleted because it is used by one or more event types.");
        }

        scheduleRepository.Remove(schedule);
        events.CollectEvent(new ScheduleDeleted(schedule.Id));

        return Result.Success();
    }
}

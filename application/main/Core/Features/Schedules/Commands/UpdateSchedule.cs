using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Schedules.Commands;

[PublicAPI]
public sealed record UpdateScheduleCommand(
    ScheduleId Id,
    string Name,
    string TimeZone,
    bool IsDefault,
    AvailabilityWindowRequest[] AvailabilityWindows,
    AvailabilityDateOverrideRequest[]? DateOverrides
) : ICommand, IRequest<Result<ScheduleResponse>>;

public sealed class UpdateScheduleValidator : AbstractValidator<UpdateScheduleCommand>
{
    public UpdateScheduleValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.TimeZone).NotEmpty().MaximumLength(100);
        RuleForEach(command => command.AvailabilityWindows).ChildRules(window =>
            {
                window.RuleFor(w => w.Days).NotEmpty().Must(days => days.All(day => day is >= 0 and <= 6)).WithMessage("Availability days must be between 0 and 6.");
                window.RuleFor(w => w.StartMinute).InclusiveBetween(0, 1439);
                window.RuleFor(w => w.EndMinute).InclusiveBetween(1, 1440);
                window.RuleFor(w => w).Must(w => w.StartMinute < w.EndMinute).WithMessage("Availability window end minute must be after start minute.");
            }
        );
        AvailabilityWindowValidator.AddAvailabilityWindowRules(RuleFor(command => command.AvailabilityWindows));
        RuleForEach(command => command.DateOverrides).ChildRules(dateOverride =>
            {
                dateOverride.RuleFor(o => o.Windows).NotNull();
                dateOverride.RuleForEach(o => o.Windows).ChildRules(window =>
                    {
                        window.RuleFor(w => w.StartMinute).InclusiveBetween(0, 1439);
                        window.RuleFor(w => w.EndMinute).InclusiveBetween(1, 1440);
                        window.RuleFor(w => w).Must(w => w.StartMinute < w.EndMinute).WithMessage("Availability date override end minute must be after start minute.");
                    }
                );
            }
        );
        AvailabilityDateOverrideValidator.AddAvailabilityDateOverrideRules(RuleFor(command => command.DateOverrides));
    }
}

public sealed class UpdateScheduleHandler(
    IScheduleRepository scheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateScheduleCommand, Result<ScheduleResponse>>
{
    public async Task<Result<ScheduleResponse>> Handle(UpdateScheduleCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result<ScheduleResponse>.Forbidden(SchedulingAuthorization.ManageSchedulesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<ScheduleResponse>.Unauthorized("Authentication is required.");
        }

        var schedule = await scheduleRepository.GetByIdAsync(command.Id, cancellationToken);
        if (schedule is null || !ScheduleAccess.HasAccess(schedule, ownerUserId, executionContext.ActiveTeamId))
        {
            return Result<ScheduleResponse>.NotFound($"Schedule '{command.Id}' was not found.");
        }

        if (!command.IsDefault && schedule.IsDefault)
        {
            var defaultSchedules = await scheduleRepository.GetDefaultCandidatesForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, schedule.Id, cancellationToken);
            if (defaultSchedules.Length == 0)
            {
                return Result<ScheduleResponse>.BadRequest("At least one default schedule is required.");
            }
        }

        if (command.IsDefault)
        {
            var defaultSchedules = await scheduleRepository.GetDefaultCandidatesForOwnerAsync(ownerUserId, executionContext.ActiveTeamId, schedule.Id, cancellationToken);
            foreach (var defaultSchedule in defaultSchedules)
            {
                defaultSchedule.SetDefault(false);
                scheduleRepository.Update(defaultSchedule);
            }
        }

        schedule.Update(
            command.Name,
            command.TimeZone,
            command.IsDefault,
            command.AvailabilityWindows.Select(window => window.ToAvailabilityWindow()).ToArray(),
            (command.DateOverrides ?? []).Select(dateOverride => dateOverride.ToAvailabilityDateOverride()).ToArray()
        );
        scheduleRepository.Update(schedule);
        events.CollectEvent(new ScheduleUpdated(schedule.Id));

        return ScheduleResponse.From(schedule);
    }
}

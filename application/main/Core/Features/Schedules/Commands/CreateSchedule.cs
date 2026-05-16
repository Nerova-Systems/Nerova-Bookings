using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Schedules.Commands;

[PublicAPI]
public sealed record CreateScheduleCommand(
    string Name,
    string TimeZone,
    bool IsDefault,
    AvailabilityWindowRequest[] AvailabilityWindows
) : ICommand, IRequest<Result<ScheduleResponse>>;

public sealed class CreateScheduleValidator : AbstractValidator<CreateScheduleCommand>
{
    public CreateScheduleValidator()
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
    }
}

public sealed class CreateScheduleHandler(
    IScheduleRepository scheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateScheduleCommand, Result<ScheduleResponse>>
{
    public async Task<Result<ScheduleResponse>> Handle(CreateScheduleCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result<ScheduleResponse>.Forbidden(SchedulingAuthorization.ManageSchedulesForbiddenMessage);
        }

        var tenantId = executionContext.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<ScheduleResponse>.Unauthorized("Authentication is required.");
        }

        var schedule = Schedule.Create(
            tenantId,
            ownerUserId,
            command.Name,
            command.TimeZone,
            command.IsDefault,
            command.AvailabilityWindows.Select(window => window.ToAvailabilityWindow()).ToArray()
        );

        if (schedule.IsDefault)
        {
            await ClearOtherDefaults(scheduleRepository, ownerUserId, null, cancellationToken);
        }

        await scheduleRepository.AddAsync(schedule, cancellationToken);
        events.CollectEvent(new ScheduleCreated(schedule.Id));

        return ScheduleResponse.From(schedule);
    }

    private static async Task ClearOtherDefaults(IScheduleRepository scheduleRepository, UserId ownerUserId, ScheduleId? excludedScheduleId, CancellationToken cancellationToken)
    {
        var defaultSchedules = await scheduleRepository.GetDefaultCandidatesForOwnerAsync(ownerUserId, excludedScheduleId, cancellationToken);
        foreach (var defaultSchedule in defaultSchedules)
        {
            defaultSchedule.SetDefault(false);
            scheduleRepository.Update(defaultSchedule);
        }
    }
}

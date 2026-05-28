using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Schedules.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.Schedule, PermissionAction.Create)]
public sealed record DuplicateScheduleCommand(ScheduleId SourceScheduleId, string NewName)
    : ICommand, IRequest<Result<ScheduleResponse>>;

public sealed class DuplicateScheduleValidator : AbstractValidator<DuplicateScheduleCommand>
{
    public DuplicateScheduleValidator()
    {
        RuleFor(command => command.NewName).NotEmpty().MaximumLength(120);
    }
}

public sealed class DuplicateScheduleHandler(
    IScheduleRepository scheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DuplicateScheduleCommand, Result<ScheduleResponse>>
{
    public async Task<Result<ScheduleResponse>> Handle(DuplicateScheduleCommand command, CancellationToken cancellationToken)
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

        var source = await scheduleRepository.GetByIdAsync(command.SourceScheduleId, cancellationToken);
        if (source is null || !ScheduleAccess.HasAccess(source, ownerUserId, executionContext.ActiveTeamId))
        {
            return Result<ScheduleResponse>.NotFound($"Schedule '{command.SourceScheduleId}' was not found.");
        }

        var duplicate = Schedule.Create(
            tenantId,
            ownerUserId,
            command.NewName,
            source.TimeZone,
            false,
            source.AvailabilityWindows.ToArray(),
            source.DateOverrides.ToArray(),
            source.TeamId
        );

        await scheduleRepository.AddAsync(duplicate, cancellationToken);
        events.CollectEvent(new ScheduleDuplicated(source.Id, duplicate.Id));

        return ScheduleResponse.From(duplicate);
    }
}

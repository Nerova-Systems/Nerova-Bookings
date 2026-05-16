using FluentValidation;
using JetBrains.Annotations;
using Main.Features;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
public sealed record CreateEventTypeCommand(
    string Title,
    string Slug,
    string? Description,
    int DurationMinutes,
    bool Hidden,
    ScheduleId ScheduleId,
    int BeforeEventBufferMinutes,
    int AfterEventBufferMinutes,
    int SlotIntervalMinutes,
    int MinimumBookingNoticeMinutes,
    string? LocationType,
    string? LocationValue
) : ICommand, IRequest<Result<EventTypeResponse>>;

public sealed class CreateEventTypeValidator : AbstractValidator<CreateEventTypeCommand>
{
    public CreateEventTypeValidator()
    {
        RuleFor(command => command.Title).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Slug)
            .NotEmpty()
            .MaximumLength(120)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must contain lowercase letters, numbers, and hyphens only.");
        RuleFor(command => command.Description).MaximumLength(1000);
        RuleFor(command => command.DurationMinutes).InclusiveBetween(5, 1440);
        RuleFor(command => command.BeforeEventBufferMinutes).InclusiveBetween(0, 1440);
        RuleFor(command => command.AfterEventBufferMinutes).InclusiveBetween(0, 1440);
        RuleFor(command => command.SlotIntervalMinutes).InclusiveBetween(5, 1440);
        RuleFor(command => command.MinimumBookingNoticeMinutes).InclusiveBetween(0, 525600);
        RuleFor(command => command.LocationType).MaximumLength(80);
        RuleFor(command => command.LocationValue).MaximumLength(500);
    }
}

public sealed class CreateEventTypeHandler(
    IEventTypeRepository eventTypeRepository,
    IScheduleRepository scheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CreateEventTypeCommand, Result<EventTypeResponse>>
{
    public async Task<Result<EventTypeResponse>> Handle(CreateEventTypeCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result<EventTypeResponse>.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var tenantId = executionContext.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<EventTypeResponse>.Unauthorized("Authentication is required.");
        }

        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId, cancellationToken);
        if (schedule is null || schedule.OwnerUserId != ownerUserId)
        {
            return Result<EventTypeResponse>.BadRequest($"Schedule '{command.ScheduleId}' was not found.");
        }

        var slug = command.Slug.Trim().ToLowerInvariant();
        if (await eventTypeRepository.SlugExistsForOwnerAsync(ownerUserId, slug, null, cancellationToken))
        {
            return Result<EventTypeResponse>.BadRequest($"An event type with slug '{slug}' already exists.");
        }

        var eventType = EventType.Create(
            tenantId,
            ownerUserId,
            command.Title,
            slug,
            command.Description,
            command.DurationMinutes,
            command.Hidden,
            command.ScheduleId,
            command.BeforeEventBufferMinutes,
            command.AfterEventBufferMinutes,
            command.SlotIntervalMinutes,
            command.MinimumBookingNoticeMinutes,
            command.LocationType,
            command.LocationValue
        );

        await eventTypeRepository.AddAsync(eventType, cancellationToken);
        events.CollectEvent(new EventTypeCreated(eventType.Id));

        return EventTypeResponse.From(eventType);
    }
}

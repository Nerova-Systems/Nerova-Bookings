using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using Main.Features.ManagedEventTypes.Shared;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Update)]
public sealed record UpdateEventTypeCommand(
    EventTypeId Id,
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
    string? LocationValue,
    EventTypeSettings? Settings = null
) : ICommand, IRequest<Result<EventTypeResponse>>;

public sealed class UpdateEventTypeValidator : AbstractValidator<UpdateEventTypeCommand>
{
    public UpdateEventTypeValidator()
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
        RuleFor(command => command.Settings!)
            .SetValidator(new EventTypeSettingsValidator())
            .When(command => command.Settings is not null);
        RuleFor(command => command)
            .Must(command => command.Settings is null ||
                             command.Settings.DurationOptions.Length == 0 ||
                             command.Settings.DurationOptions.Contains(command.DurationMinutes)
            )
            .WithMessage("Duration options must include the primary duration.");
    }
}

public sealed class UpdateEventTypeHandler(
    IEventTypeRepository eventTypeRepository,
    IScheduleRepository scheduleRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateEventTypeCommand, Result<EventTypeResponse>>
{
    public async Task<Result<EventTypeResponse>> Handle(UpdateEventTypeCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result<EventTypeResponse>.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<EventTypeResponse>.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(command.Id, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result<EventTypeResponse>.NotFound($"Event type '{command.Id}' was not found.");
        }

        var slug = command.Slug.Trim().ToLowerInvariant();
        if (eventType.ParentEventTypeId is not null)
        {
            var changedFields = GetChangedManagedFields(command, eventType, slug).ToArray();
            var lockedFields = changedFields
                .Where(f => !eventType.UnlockedFields.Contains(f, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (lockedFields.Length > 0)
            {
                foreach (var field in lockedFields)
                {
                    events.CollectEvent(new ManagedEventTypeFieldOverrideRejected(eventType.Id, field));
                }

                return Result<EventTypeResponse>.Forbidden($"Fields {string.Join(", ", lockedFields)} are locked by the managed template.");
            }
        }

        var schedule = await scheduleRepository.GetByIdAsync(command.ScheduleId, cancellationToken);
        if (schedule is null || schedule.OwnerUserId != ownerUserId)
        {
            return Result<EventTypeResponse>.BadRequest($"Schedule '{command.ScheduleId}' was not found.");
        }

        if (await eventTypeRepository.SlugExistsForOwnerAsync(ownerUserId, slug, eventType.Id, cancellationToken))
        {
            return Result<EventTypeResponse>.BadRequest($"An event type with slug '{slug}' already exists.");
        }

        eventType.Update(
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
            command.LocationValue,
            command.Settings
        );
        eventTypeRepository.Update(eventType);
        events.CollectEvent(new EventTypeUpdated(eventType.Id));

        return EventTypeResponse.From(eventType);
    }

    private static IEnumerable<string> GetChangedManagedFields(UpdateEventTypeCommand command, EventType eventType, string normalizedSlug)
    {
        if (command.Title.Trim() != eventType.Title)
        {
            yield return ManagedEventTypeFields.Title;
        }

        if (normalizedSlug != eventType.Slug)
        {
            yield return ManagedEventTypeFields.Slug;
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        if (normalizedDescription != eventType.Description)
        {
            yield return ManagedEventTypeFields.Description;
        }

        if (command.DurationMinutes != eventType.DurationMinutes)
        {
            yield return ManagedEventTypeFields.DurationMinutes;
        }

        if (command.Hidden != eventType.Hidden)
        {
            yield return ManagedEventTypeFields.Hidden;
        }

        if (command.ScheduleId != eventType.ScheduleId)
        {
            yield return ManagedEventTypeFields.ScheduleId;
        }

        if (command.BeforeEventBufferMinutes != eventType.BeforeEventBufferMinutes)
        {
            yield return ManagedEventTypeFields.BeforeEventBufferMinutes;
        }

        if (command.AfterEventBufferMinutes != eventType.AfterEventBufferMinutes)
        {
            yield return ManagedEventTypeFields.AfterEventBufferMinutes;
        }

        if (command.SlotIntervalMinutes != eventType.SlotIntervalMinutes)
        {
            yield return ManagedEventTypeFields.SlotIntervalMinutes;
        }

        if (command.MinimumBookingNoticeMinutes != eventType.MinimumBookingNoticeMinutes)
        {
            yield return ManagedEventTypeFields.MinimumBookingNoticeMinutes;
        }

        var normalizedLocationType = string.IsNullOrWhiteSpace(command.LocationType) ? null : command.LocationType.Trim();
        if (normalizedLocationType != eventType.LocationType)
        {
            yield return ManagedEventTypeFields.LocationType;
        }

        var normalizedLocationValue = string.IsNullOrWhiteSpace(command.LocationValue) ? null : command.LocationValue.Trim();
        if (normalizedLocationValue != eventType.LocationValue)
        {
            yield return ManagedEventTypeFields.LocationValue;
        }

        var normalizedSettings = EventTypeSettings.Normalize(command.Settings, command.DurationMinutes, normalizedLocationType, normalizedLocationValue);
        if (System.Text.Json.JsonSerializer.Serialize(normalizedSettings) != System.Text.Json.JsonSerializer.Serialize(eventType.Settings))
        {
            yield return ManagedEventTypeFields.Settings;
        }
    }
}

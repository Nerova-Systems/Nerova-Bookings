using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
public sealed record UpdateTeamAssignmentCommand(
    EventTypeId Id,
    bool AssignAllTeamMembers,
    EventTypeTeamAssignment? TeamAssignment = null
) : ICommand, IRequest<Result<EventTypeResponse>>;

public sealed class UpdateTeamAssignmentValidator : AbstractValidator<UpdateTeamAssignmentCommand>
{
    public UpdateTeamAssignmentValidator()
    {
        RuleFor(command => command.TeamAssignment!.MaxLeadThreshold)
            .GreaterThan(0)
            .When(command => command.TeamAssignment?.MaxLeadThreshold is not null);
    }
}

public sealed class UpdateTeamAssignmentHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateTeamAssignmentCommand, Result<EventTypeResponse>>
{
    public async Task<Result<EventTypeResponse>> Handle(UpdateTeamAssignmentCommand command, CancellationToken cancellationToken)
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

        eventType.SetAssignAllTeamMembers(command.AssignAllTeamMembers);

        if (command.TeamAssignment is not null)
        {
            var settings = eventType.Settings with { TeamAssignment = command.TeamAssignment };
            eventType.SetSettings(settings);
        }

        events.CollectEvent(new TeamAssignmentUpdated(eventType.Id, command.AssignAllTeamMembers));

        return EventTypeResponse.From(eventType);
    }
}

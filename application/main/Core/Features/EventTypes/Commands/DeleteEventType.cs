using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Delete)]
public sealed record DeleteEventTypeCommand(EventTypeId Id) : ICommand, IRequest<Result>;

public sealed class DeleteEventTypeHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteEventTypeCommand, Result>
{
    public async Task<Result> Handle(DeleteEventTypeCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var eventType = await eventTypeRepository.GetByIdAsync(command.Id, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result.NotFound($"Event type '{command.Id}' was not found.");
        }

        // If this is a managed template, soft-delete all child replicas first.
        var children = await eventTypeRepository.GetChildrenAsync(command.Id, cancellationToken);
        foreach (var child in children)
        {
            eventTypeRepository.Remove(child);
        }

        eventTypeRepository.Remove(eventType);
        events.CollectEvent(new EventTypeDeleted(eventType.Id));

        return Result.Success();
    }
}

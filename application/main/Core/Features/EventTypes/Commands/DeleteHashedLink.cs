using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
public sealed record DeleteHashedLinkCommand(EventTypeId EventTypeId, HashedLinkId HashedLinkId) : ICommand, IRequest<Result>;

public sealed class DeleteHashedLinkHandler(
    IEventTypeRepository eventTypeRepository,
    IHashedLinkRepository hashedLinkRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<DeleteHashedLinkCommand, Result>
{
    public async Task<Result> Handle(DeleteHashedLinkCommand command, CancellationToken cancellationToken)
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

        var eventType = await eventTypeRepository.GetByIdAsync(command.EventTypeId, cancellationToken);
        if (eventType is null || eventType.OwnerUserId != ownerUserId)
        {
            return Result.NotFound($"Event type '{command.EventTypeId}' was not found.");
        }

        var link = await hashedLinkRepository.GetByIdAsync(command.HashedLinkId, cancellationToken);
        if (link is null || link.EventTypeId != command.EventTypeId)
        {
            return Result.NotFound($"Hashed link '{command.HashedLinkId}' was not found.");
        }

        hashedLinkRepository.Remove(link);
        events.CollectEvent(new HashedLinkDeleted(eventType.Id, link.Id));

        return Result.Success();
    }
}

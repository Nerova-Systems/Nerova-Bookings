using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Collective.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Collective.Commands.AddCollectiveHost;

[PublicAPI]
public sealed record AddCollectiveHostCommand(EventTypeId EventTypeId, UserId UserId) : ICommand, IRequest<Result>;

public sealed class AddCollectiveHostValidator : AbstractValidator<AddCollectiveHostCommand>
{
    public AddCollectiveHostValidator()
    {
        RuleFor(command => command.UserId.Value).NotEmpty().WithMessage("User ID is required.");
    }
}

public sealed class AddCollectiveHostHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<AddCollectiveHostCommand, Result>
{
    public async Task<Result> Handle(AddCollectiveHostCommand command, CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!CollectiveAuthorization.HasCollectiveFeature(userInfo))
        {
            return Result.Forbidden(CollectiveAuthorization.CollectiveFeatureDisabledMessage);
        }

        if (!CollectiveAuthorization.CanManageCollectiveHosts(userInfo))
        {
            return Result.Forbidden(CollectiveAuthorization.ManageCollectiveHostsForbiddenMessage);
        }

        var eventType = await eventTypeRepository.GetByIdAsync(command.EventTypeId, cancellationToken);
        if (eventType is null)
        {
            return Result.NotFound($"Event type '{command.EventTypeId}' was not found.");
        }

        if (eventType.TenantId != userInfo.TenantId)
        {
            return Result.NotFound($"Event type '{command.EventTypeId}' was not found.");
        }

        if (eventType.TeamId is null)
        {
            return Result.BadRequest("Collective hosts can only be added to team-scoped event types.");
        }

        var existing = await hostRepository.GetByEventTypeAndUserAsync(command.EventTypeId, command.UserId, cancellationToken);
        if (existing is not null)
        {
            return Result.Conflict($"User '{command.UserId}' is already a host for this event type.");
        }

        var setSchedulingTypeResult = eventType.SetSchedulingType(SchedulingType.Collective);
        if (!setSchedulingTypeResult.IsSuccess)
        {
            return setSchedulingTypeResult;
        }

        eventTypeRepository.Update(eventType);
        var host = Host.Create(
            eventType.TenantId,
            command.EventTypeId,
            command.UserId,
            true,
            0,
            100
        );
        await hostRepository.AddAsync(host, cancellationToken);
        events.CollectEvent(new CollectiveHostAdded(command.EventTypeId, command.UserId));

        return Result.Success();
    }
}

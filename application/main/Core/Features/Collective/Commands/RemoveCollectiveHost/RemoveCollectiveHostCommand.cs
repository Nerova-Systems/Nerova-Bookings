using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Collective.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Collective.Commands.RemoveCollectiveHost;

[PublicAPI]
public sealed record RemoveCollectiveHostCommand(EventTypeId EventTypeId, UserId UserId) : ICommand, IRequest<Result>;

public sealed class RemoveCollectiveHostValidator : AbstractValidator<RemoveCollectiveHostCommand>
{
    public RemoveCollectiveHostValidator()
    {
        RuleFor(command => command.UserId.Value).NotEmpty().WithMessage("User ID is required.");
    }
}

public sealed class RemoveCollectiveHostHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<RemoveCollectiveHostCommand, Result>
{
    public async Task<Result> Handle(RemoveCollectiveHostCommand command, CancellationToken cancellationToken)
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

        var host = await hostRepository.GetByEventTypeAndUserAsync(command.EventTypeId, command.UserId, cancellationToken);
        if (host is null)
        {
            return Result.NotFound($"User '{command.UserId}' is not a host for this event type.");
        }

        hostRepository.Remove(host);
        events.CollectEvent(new CollectiveHostRemoved(command.EventTypeId, command.UserId));

        return Result.Success();
    }
}

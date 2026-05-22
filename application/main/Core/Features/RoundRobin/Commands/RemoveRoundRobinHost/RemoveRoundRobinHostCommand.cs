using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.RoundRobin.Commands.RemoveRoundRobinHost;

[PublicAPI]
public sealed record RemoveRoundRobinHostCommand(EventTypeId EventTypeId, UserId UserId) : ICommand, IRequest<Result>;

public sealed class RemoveRoundRobinHostValidator : AbstractValidator<RemoveRoundRobinHostCommand>
{
    public RemoveRoundRobinHostValidator()
    {
        RuleFor(command => command.UserId.Value).NotEmpty().WithMessage("User ID is required.");
    }
}

public sealed class RemoveRoundRobinHostHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<RemoveRoundRobinHostCommand, Result>
{
    public async Task<Result> Handle(RemoveRoundRobinHostCommand command, CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!RoundRobinAuthorization.HasRoundRobinFeature(userInfo))
        {
            return Result.Forbidden(RoundRobinAuthorization.RoundRobinFeatureDisabledMessage);
        }

        if (!RoundRobinAuthorization.CanManageRoundRobinHosts(userInfo))
        {
            return Result.Forbidden(RoundRobinAuthorization.ManageRoundRobinHostsForbiddenMessage);
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
        events.CollectEvent(new RoundRobinHostRemoved(command.EventTypeId, command.UserId));

        return Result.Success();
    }
}

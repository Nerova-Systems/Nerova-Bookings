using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.RoundRobin.Commands.UpdateRoundRobinHost;

[PublicAPI]
public sealed record UpdateRoundRobinHostCommand(EventTypeId EventTypeId, UserId UserId, bool IsFixed, int Priority, int Weight)
    : ICommand, IRequest<Result>;

public sealed class UpdateRoundRobinHostValidator : AbstractValidator<UpdateRoundRobinHostCommand>
{
    public UpdateRoundRobinHostValidator()
    {
        RuleFor(command => command.UserId.Value).NotEmpty().WithMessage("User ID is required.");
        RuleFor(command => command.Priority).GreaterThanOrEqualTo(0).WithMessage("Priority must be 0 or greater.");
        RuleFor(command => command.Weight).InclusiveBetween(1, 1000).WithMessage("Weight must be between 1 and 1000.");
    }
}

public sealed class UpdateRoundRobinHostHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateRoundRobinHostCommand, Result>
{
    public async Task<Result> Handle(UpdateRoundRobinHostCommand command, CancellationToken cancellationToken)
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

        host.Update(command.IsFixed, command.Priority, command.Weight);
        hostRepository.Update(host);
        events.CollectEvent(new RoundRobinHostUpdated(command.EventTypeId, command.UserId));

        return Result.Success();
    }
}

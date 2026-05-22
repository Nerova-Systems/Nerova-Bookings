using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.RoundRobin.Commands.AddRoundRobinHost;

[PublicAPI]
public sealed record AddRoundRobinHostCommand(EventTypeId EventTypeId, UserId UserId, bool IsFixed, int Priority, int Weight)
    : ICommand, IRequest<Result>;

public sealed class AddRoundRobinHostValidator : AbstractValidator<AddRoundRobinHostCommand>
{
    public AddRoundRobinHostValidator()
    {
        RuleFor(command => command.UserId.Value).NotEmpty().WithMessage("User ID is required.");
        RuleFor(command => command.Priority).GreaterThanOrEqualTo(0).WithMessage("Priority must be 0 or greater.");
        RuleFor(command => command.Weight).InclusiveBetween(1, 1000).WithMessage("Weight must be between 1 and 1000.");
    }
}

public sealed class AddRoundRobinHostHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<AddRoundRobinHostCommand, Result>
{
    public async Task<Result> Handle(AddRoundRobinHostCommand command, CancellationToken cancellationToken)
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

        if (eventType.TeamId is null)
        {
            return Result.BadRequest("Round-robin hosts can only be added to team-scoped event types.");
        }

        var existing = await hostRepository.GetByEventTypeAndUserAsync(command.EventTypeId, command.UserId, cancellationToken);
        if (existing is not null)
        {
            return Result.Conflict($"User '{command.UserId}' is already a host for this event type.");
        }

        var setSchedulingTypeResult = eventType.SetSchedulingType(SchedulingType.RoundRobin);
        if (!setSchedulingTypeResult.IsSuccess)
        {
            return setSchedulingTypeResult;
        }

        eventTypeRepository.Update(eventType);
        var host = Host.Create(
            eventType.TenantId,
            command.EventTypeId,
            command.UserId,
            command.IsFixed,
            command.Priority,
            command.Weight
        );
        await hostRepository.AddAsync(host, cancellationToken);
        events.CollectEvent(new RoundRobinHostAdded(command.EventTypeId, command.UserId));

        return Result.Success();
    }
}

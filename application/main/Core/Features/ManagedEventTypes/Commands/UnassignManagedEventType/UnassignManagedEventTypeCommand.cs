using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.ManagedEventTypes.Commands.UnassignManagedEventType;

[PublicAPI]
public sealed record UnassignManagedEventTypeCommand(EventTypeId ParentId, UserId MemberUserId) : ICommand, IRequest<Result>;

public sealed class UnassignManagedEventTypeValidator : AbstractValidator<UnassignManagedEventTypeCommand>
{
    public UnassignManagedEventTypeValidator()
    {
        RuleFor(command => command.MemberUserId.Value).NotEmpty().WithMessage("Member user ID is required.");
    }
}

public sealed class UnassignManagedEventTypeHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UnassignManagedEventTypeCommand, Result>
{
    public async Task<Result> Handle(UnassignManagedEventTypeCommand command, CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!ManagedEventTypeAuthorization.HasManagedEventTypesFeature(userInfo))
        {
            return Result.Forbidden(ManagedEventTypeAuthorization.ManagedEventTypesFeatureDisabledMessage);
        }

        if (!ManagedEventTypeAuthorization.CanManageManagedEventTypes(userInfo))
        {
            return Result.Forbidden(ManagedEventTypeAuthorization.ManageManagedEventTypesForbiddenMessage);
        }

        var parent = await eventTypeRepository.GetByIdAsync(command.ParentId, cancellationToken);
        if (parent is null)
        {
            return Result.NotFound($"Event type '{command.ParentId}' was not found.");
        }

        var child = await eventTypeRepository.GetChildByParentAndMemberAsync(command.ParentId, command.MemberUserId, cancellationToken);
        if (child is null)
        {
            return Result.NotFound($"Member '{command.MemberUserId}' is not assigned to this managed event type.");
        }

        eventTypeRepository.Remove(child);
        events.CollectEvent(new ManagedEventTypeUnassigned(parent.Id, child.Id, command.MemberUserId));

        return Result.Success();
    }
}

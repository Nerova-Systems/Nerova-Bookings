using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.ManagedEventTypes.Commands.AssignManagedEventType;

[PublicAPI]
public sealed record AssignManagedEventTypeCommand(EventTypeId ParentId, UserId MemberUserId) : ICommand, IRequest<Result>;

public sealed class AssignManagedEventTypeValidator : AbstractValidator<AssignManagedEventTypeCommand>
{
    public AssignManagedEventTypeValidator()
    {
        RuleFor(command => command.MemberUserId.Value).NotEmpty().WithMessage("Member user ID is required.");
    }
}

public sealed class AssignManagedEventTypeHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<AssignManagedEventTypeCommand, Result>
{
    public async Task<Result> Handle(AssignManagedEventTypeCommand command, CancellationToken cancellationToken)
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

        var canBeTemplate = parent.EnsureCanBeManagedTemplate();
        if (!canBeTemplate.IsSuccess)
        {
            return canBeTemplate;
        }

        // Guard: member must not already be assigned.
        var existing = await eventTypeRepository.GetChildByParentAndMemberAsync(command.ParentId, command.MemberUserId, cancellationToken);
        if (existing is not null)
        {
            return Result.Conflict($"Member '{command.MemberUserId}' is already assigned to this managed event type.");
        }

        var child = parent.CreateChildReplica(command.MemberUserId);
        await eventTypeRepository.AddAsync(child, cancellationToken);
        events.CollectEvent(new ManagedEventTypeAssigned(parent.Id, child.Id, command.MemberUserId));

        return Result.Success();
    }
}

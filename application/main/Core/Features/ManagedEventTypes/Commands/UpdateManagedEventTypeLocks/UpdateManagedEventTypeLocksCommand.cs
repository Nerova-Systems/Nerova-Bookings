using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Services;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.ManagedEventTypes.Commands.UpdateManagedEventTypeLocks;

[PublicAPI]
public sealed record UpdateManagedEventTypeLocksCommand(EventTypeId ParentId, string[] UnlockedFields)
    : ICommand, IRequest<Result>;

public sealed class UpdateManagedEventTypeLocksValidator : AbstractValidator<UpdateManagedEventTypeLocksCommand>
{
    public UpdateManagedEventTypeLocksValidator()
    {
        RuleFor(command => command.UnlockedFields)
            .Must(fields => fields.All(f => ManagedEventTypeFields.All.Contains(f, StringComparer.OrdinalIgnoreCase)))
            .When(command => command.UnlockedFields.Length > 0)
            .WithMessage(command => $"Unknown fields: {string.Join(", ", command.UnlockedFields.Where(f => !ManagedEventTypeFields.All.Contains(f, StringComparer.OrdinalIgnoreCase)))}.");
    }
}

public sealed class UpdateManagedEventTypeLocksHandler(
    IEventTypeRepository eventTypeRepository,
    ManagedEventTypePropagator propagator,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateManagedEventTypeLocksCommand, Result>
{
    public async Task<Result> Handle(UpdateManagedEventTypeLocksCommand command, CancellationToken cancellationToken)
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

        if (parent.ParentEventTypeId is not null)
        {
            return Result.BadRequest("Only parent templates support lock configuration.");
        }

        parent.UpdateUnlockedFields(command.UnlockedFields);
        eventTypeRepository.Update(parent);

        // Re-propagate so newly locked fields snap back to parent values on all children.
        await propagator.PropagateAsync(parent, eventTypeRepository, cancellationToken);

        events.CollectEvent(new ManagedEventTypeLocksUpdated(parent.Id, parent.UnlockedFields.Length));

        return Result.Success();
    }
}

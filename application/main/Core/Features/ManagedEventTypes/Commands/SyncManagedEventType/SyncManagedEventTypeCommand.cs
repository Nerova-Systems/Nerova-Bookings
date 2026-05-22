using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.EventHandlers;
using Main.Features.ManagedEventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.ManagedEventTypes.Commands.SyncManagedEventType;

[PublicAPI]
public sealed record SyncManagedEventTypeCommand(EventTypeId ParentId) : ICommand, IRequest<Result>;

public sealed class SyncManagedEventTypeValidator : AbstractValidator<SyncManagedEventTypeCommand>
{
    public SyncManagedEventTypeValidator()
    {
        RuleFor(command => command.ParentId.Value).NotEmpty().WithMessage("Parent event type ID is required.");
    }
}

public sealed class SyncManagedEventTypeHandler(
    IEventTypeRepository eventTypeRepository,
    EventTypeUpdatedManagedSyncHandler syncHandler,
    IExecutionContext executionContext
) : IRequestHandler<SyncManagedEventTypeCommand, Result>
{
    public async Task<Result> Handle(SyncManagedEventTypeCommand command, CancellationToken cancellationToken)
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
            return Result.BadRequest("Only parent templates can be synced.");
        }

        await syncHandler.SyncChildrenAsync(command.ParentId, cancellationToken);

        return Result.Success();
    }
}

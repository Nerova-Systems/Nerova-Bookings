using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.EventTypes.Commands;

[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Update)]
public sealed record BulkApplyLocationsCommand(BulkApplyLocationsItem[] Items)
    : ICommand, IRequest<Result<BulkApplyLocationsResponse>>;

public sealed class BulkApplyLocationsValidator : AbstractValidator<BulkApplyLocationsCommand>
{
    public BulkApplyLocationsValidator()
    {
        RuleFor(command => command.Items).NotEmpty();
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.LocationType).MaximumLength(80);
            item.RuleFor(value => value.LocationValue).MaximumLength(500);
        });
    }
}

/// <summary>
///     Atomically applies a new location to one or many event types owned by the caller.
///     Wrapped in the UnitOfWork pipeline behavior: if any item fails authorization or
///     lookup, the whole batch is aborted and no changes are persisted.
/// </summary>
public sealed class BulkApplyLocationsHandler(
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<BulkApplyLocationsCommand, Result<BulkApplyLocationsResponse>>
{
    public async Task<Result<BulkApplyLocationsResponse>> Handle(BulkApplyLocationsCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result<BulkApplyLocationsResponse>.Forbidden(SchedulingAuthorization.ManageEventTypesForbiddenMessage);
        }

        var ownerUserId = executionContext.UserInfo.Id;
        if (ownerUserId is null)
        {
            return Result<BulkApplyLocationsResponse>.Unauthorized("Authentication is required.");
        }

        var ids = command.Items.Select(item => item.EventTypeId).Distinct().ToArray();
        var eventTypes = await eventTypeRepository.GetByIdsAsync(ids, cancellationToken);
        var indexed = eventTypes.ToDictionary(eventType => eventType.Id);

        foreach (var item in command.Items)
        {
            if (!indexed.TryGetValue(item.EventTypeId, out var eventType) || eventType.OwnerUserId != ownerUserId)
            {
                return Result<BulkApplyLocationsResponse>.NotFound($"Event type '{item.EventTypeId}' was not found.");
            }

            eventType.SetLocation(item.LocationType, item.LocationValue);
            eventTypeRepository.Update(eventType);
            events.CollectEvent(new EventTypeUpdated(eventType.Id));
        }

        return new BulkApplyLocationsResponse(ids);
    }
}

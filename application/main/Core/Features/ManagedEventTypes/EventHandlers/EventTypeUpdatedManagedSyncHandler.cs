using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Services;
using SharedKernel.Telemetry;

namespace Main.Features.ManagedEventTypes.EventHandlers;

/// <summary>
///     Syncs child replicas whenever a managed parent template is explicitly updated.
///     Called directly from <see cref="Commands.SyncManagedEventType.SyncManagedEventTypeHandler" />.
/// </summary>
public sealed class EventTypeUpdatedManagedSyncHandler(
    IEventTypeRepository repository,
    ManagedEventTypePropagator propagator,
    ITelemetryEventsCollector events
)
{
    /// <summary>
    ///     Propagates the parent's current values to all children (locked fields only).
    ///     Returns the number of children updated, or 0 if the event type is not a parent template.
    /// </summary>
    public async Task<int> SyncChildrenAsync(EventTypeId parentId, CancellationToken cancellationToken)
    {
        var parent = await repository.GetByIdAsync(parentId, cancellationToken);
        if (parent is null || parent.ParentEventTypeId is not null)
        {
            // Not a parent template — nothing to sync.
            return 0;
        }

        var childCount = await propagator.PropagateAsync(parent, repository, cancellationToken);
        events.CollectEvent(new ManagedEventTypeSynced(parentId, childCount));
        return childCount;
    }
}

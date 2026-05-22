using Main.Features.EventTypes.Domain;

namespace Main.Features.ManagedEventTypes.Services;

/// <summary>
///     Propagates locked field values from a parent managed event type template to all its child replicas.
/// </summary>
public sealed class ManagedEventTypePropagator
{
    /// <summary>
    ///     Loads all children of <paramref name="parent" /> and applies propagation for each locked field.
    ///     Changes are staged via the repository but not yet committed (the caller's Unit of Work handles saving).
    /// </summary>
    public async Task<int> PropagateAsync(
        EventType parent,
        IEventTypeRepository repository,
        CancellationToken cancellationToken)
    {
        var children = await repository.GetChildrenAsync(parent.Id, cancellationToken);
        foreach (var child in children)
        {
            child.PropagateFromParent(parent);
            repository.Update(child);
        }

        return children.Length;
    }
}

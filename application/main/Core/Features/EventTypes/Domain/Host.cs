using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.EventTypes.Domain;

[IdPrefix("host")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, HostId>))]
public sealed record HostId(string Value) : StronglyTypedUlid<HostId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Represents a host on a team event type.
///     Used by both Collective (all hosts required) and RoundRobin (rotation) scheduling types.
/// </summary>
public sealed class Host : AggregateRoot<HostId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private Host() : base(HostId.NewId())
    {
        EventTypeId = new EventTypeId(string.Empty);
        UserId = new UserId(string.Empty);
    }

    private Host(
        TenantId tenantId,
        EventTypeId eventTypeId,
        UserId userId,
        bool isFixed,
        int priority,
        int weight
    ) : base(HostId.NewId())
    {
        TenantId = tenantId;
        EventTypeId = eventTypeId;
        UserId = userId;
        IsFixed = isFixed;
        Priority = priority;
        Weight = weight;
    }

    public TenantId TenantId { get; } = new(0);

    public EventTypeId EventTypeId { get; private set; }

    public UserId UserId { get; private set; }

    /// <summary>
    ///     When true, this host is always included for every booking (collective always sets this to true).
    ///     For round-robin, false means the host participates in rotation but is not required.
    /// </summary>
    public bool IsFixed { get; private set; }

    /// <summary>Round-robin priority ordering (lower = higher priority). Unused for collective.</summary>
    public int Priority { get; private set; }

    /// <summary>Round-robin weight (higher = more bookings). Unused for collective.</summary>
    public int Weight { get; private set; }

    public static Host Create(
        TenantId tenantId,
        EventTypeId eventTypeId,
        UserId userId,
        bool isFixed,
        int priority,
        int weight
    )
    {
        return new Host(tenantId, eventTypeId, userId, isFixed, priority, weight);
    }
}

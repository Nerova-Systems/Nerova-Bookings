using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;

namespace Main.Features.RoundRobin.Shared;

[PublicAPI]
public sealed record RoundRobinHostResponse(
    EventTypeId EventTypeId,
    UserId UserId,
    bool IsFixed,
    int Priority,
    int Weight
)
{
    public static RoundRobinHostResponse From(Host host)
    {
        return new RoundRobinHostResponse(
            host.EventTypeId,
            host.UserId,
            host.IsFixed,
            host.Priority,
            host.Weight
        );
    }
}

[PublicAPI]
public sealed record RoundRobinHostsResponse(RoundRobinHostResponse[] Hosts);

[PublicAPI]
public sealed record AddRoundRobinHostRequest(UserId UserId, bool IsFixed = false, int Priority = 0, int Weight = 100);

[PublicAPI]
public sealed record RemoveRoundRobinHostRequest(UserId UserId);

[PublicAPI]
public sealed record UpdateRoundRobinHostRequest(bool IsFixed, int Priority, int Weight);

[PublicAPI]
public sealed record ReassignRoundRobinBookingRequest(UserId NewOwnerUserId);

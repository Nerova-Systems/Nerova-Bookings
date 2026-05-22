using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using SharedKernel.Domain;

namespace Main.Features.Collective.Shared;

[PublicAPI]
public sealed record CollectiveHostResponse(
    EventTypeId EventTypeId,
    UserId UserId,
    bool IsFixed,
    int Priority,
    int Weight
)
{
    public static CollectiveHostResponse From(Host host)
    {
        return new CollectiveHostResponse(
            host.EventTypeId,
            host.UserId,
            host.IsFixed,
            host.Priority,
            host.Weight
        );
    }
}

[PublicAPI]
public sealed record CollectiveHostsResponse(CollectiveHostResponse[] Hosts);

[PublicAPI]
public sealed record AddCollectiveHostRequest(UserId UserId);

[PublicAPI]
public sealed record RemoveCollectiveHostRequest(UserId UserId);

using JetBrains.Annotations;
using Main.Features.Collective.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Collective.Queries.ListCollectiveHosts;

[PublicAPI]
public sealed record ListCollectiveHostsQuery(EventTypeId EventTypeId) : IRequest<Result<CollectiveHostsResponse>>;

public sealed class ListCollectiveHostsHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext
) : IRequestHandler<ListCollectiveHostsQuery, Result<CollectiveHostsResponse>>
{
    public async Task<Result<CollectiveHostsResponse>> Handle(ListCollectiveHostsQuery query, CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!CollectiveAuthorization.HasCollectiveFeature(userInfo))
        {
            return Result<CollectiveHostsResponse>.Forbidden(CollectiveAuthorization.CollectiveFeatureDisabledMessage);
        }

        var eventType = await eventTypeRepository.GetByIdAsync(query.EventTypeId, cancellationToken);
        if (eventType is null)
        {
            return Result<CollectiveHostsResponse>.NotFound($"Event type '{query.EventTypeId}' was not found.");
        }

        if (eventType.TenantId != userInfo.TenantId)
        {
            return Result<CollectiveHostsResponse>.NotFound($"Event type '{query.EventTypeId}' was not found.");
        }

        var hosts = await hostRepository.GetForEventTypeAsync(query.EventTypeId, cancellationToken);
        var response = new CollectiveHostsResponse(hosts.Select(CollectiveHostResponse.From).ToArray());

        return response;
    }
}

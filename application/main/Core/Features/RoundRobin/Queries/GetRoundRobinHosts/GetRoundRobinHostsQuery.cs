using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.RoundRobin.Queries.GetRoundRobinHosts;

[PublicAPI]
public sealed record GetRoundRobinHostsQuery(EventTypeId EventTypeId) : IRequest<Result<RoundRobinHostsResponse>>;

public sealed class GetRoundRobinHostsHandler(
    IEventTypeRepository eventTypeRepository,
    IHostRepository hostRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetRoundRobinHostsQuery, Result<RoundRobinHostsResponse>>
{
    public async Task<Result<RoundRobinHostsResponse>> Handle(GetRoundRobinHostsQuery query, CancellationToken cancellationToken)
    {
        var userInfo = executionContext.UserInfo;

        if (!RoundRobinAuthorization.HasRoundRobinFeature(userInfo))
        {
            return Result<RoundRobinHostsResponse>.Forbidden(RoundRobinAuthorization.RoundRobinFeatureDisabledMessage);
        }

        var eventType = await eventTypeRepository.GetByIdAsync(query.EventTypeId, cancellationToken);
        if (eventType is null)
        {
            return Result<RoundRobinHostsResponse>.NotFound($"Event type '{query.EventTypeId}' was not found.");
        }

        if (eventType.TenantId != userInfo.TenantId)
        {
            return Result<RoundRobinHostsResponse>.NotFound($"Event type '{query.EventTypeId}' was not found.");
        }

        var hosts = await hostRepository.GetForEventTypeAsync(query.EventTypeId, cancellationToken);
        var response = new RoundRobinHostsResponse(hosts.Select(RoundRobinHostResponse.From).ToArray());

        return response;
    }
}

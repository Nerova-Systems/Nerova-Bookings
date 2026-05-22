using Main.Features.Collective.Commands.AddCollectiveHost;
using Main.Features.Collective.Commands.RemoveCollectiveHost;
using Main.Features.Collective.Queries.ListCollectiveHosts;
using Main.Features.Collective.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.ApiResults;
using SharedKernel.Domain;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class CollectiveEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/collective";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Collective").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/{eventTypeId}/hosts", async Task<ApiResult<CollectiveHostsResponse>> (EventTypeId eventTypeId, IMediator mediator)
            => await mediator.Send(new ListCollectiveHostsQuery(eventTypeId))
        ).Produces<CollectiveHostsResponse>();

        group.MapPost("/{eventTypeId}/hosts", async Task<ApiResult> (EventTypeId eventTypeId, AddCollectiveHostRequest request, IMediator mediator)
            => await mediator.Send(new AddCollectiveHostCommand(eventTypeId, request.UserId))
        );

        group.MapDelete("/{eventTypeId}/hosts/{userId}", async Task<ApiResult> (EventTypeId eventTypeId, UserId userId, IMediator mediator)
            => await mediator.Send(new RemoveCollectiveHostCommand(eventTypeId, userId))
        );
    }
}

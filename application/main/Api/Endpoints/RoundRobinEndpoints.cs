using Main.Features.EventTypes.Domain;
using Main.Features.RoundRobin.Commands.AddRoundRobinHost;
using Main.Features.RoundRobin.Commands.ReassignRoundRobinBooking;
using Main.Features.RoundRobin.Commands.RemoveRoundRobinHost;
using Main.Features.RoundRobin.Commands.UpdateRoundRobinHost;
using Main.Features.RoundRobin.Queries.GetRoundRobinHosts;
using Main.Features.RoundRobin.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.ApiResults;
using SharedKernel.Domain;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class RoundRobinEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/round-robin";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("RoundRobin").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/{eventTypeId}/hosts", async Task<ApiResult<RoundRobinHostsResponse>> (EventTypeId eventTypeId, IMediator mediator)
            => await mediator.Send(new GetRoundRobinHostsQuery(eventTypeId))
        ).Produces<RoundRobinHostsResponse>();

        group.MapPost("/{eventTypeId}/hosts", async Task<ApiResult> (EventTypeId eventTypeId, AddRoundRobinHostRequest request, IMediator mediator)
            => await mediator.Send(new AddRoundRobinHostCommand(eventTypeId, request.UserId, request.IsFixed, request.Priority, request.Weight))
        );

        group.MapPut("/{eventTypeId}/hosts/{userId}", async Task<ApiResult> (EventTypeId eventTypeId, UserId userId, UpdateRoundRobinHostRequest request, IMediator mediator)
            => await mediator.Send(new UpdateRoundRobinHostCommand(eventTypeId, userId, request.IsFixed, request.Priority, request.Weight))
        );

        group.MapDelete("/{eventTypeId}/hosts/{userId}", async Task<ApiResult> (EventTypeId eventTypeId, UserId userId, IMediator mediator)
            => await mediator.Send(new RemoveRoundRobinHostCommand(eventTypeId, userId))
        );

        group.MapPost("/bookings/{bookingId}/reassign", async Task<ApiResult> (BookingId bookingId, ReassignRoundRobinBookingRequest request, IMediator mediator)
            => await mediator.Send(new ReassignRoundRobinBookingCommand(bookingId, request.NewOwnerUserId))
        );
    }
}

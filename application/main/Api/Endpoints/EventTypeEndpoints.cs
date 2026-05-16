using Main.Features.EventTypes.Commands;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Queries;
using Main.Features.EventTypes.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class EventTypeEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/event-types";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("EventTypes").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<EventTypesResponse>> (IMediator mediator)
            => await mediator.Send(new GetEventTypesQuery())
        ).Produces<EventTypesResponse>();

        group.MapGet("/{id}", async Task<ApiResult<EventTypeResponse>> (EventTypeId id, IMediator mediator)
            => await mediator.Send(new GetEventTypeQuery(id))
        ).Produces<EventTypeResponse>();

        group.MapPost("/", async Task<ApiResult<EventTypeResponse>> (CreateEventTypeCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<EventTypeResponse>();

        group.MapPut("/{id}", async Task<ApiResult<EventTypeResponse>> (EventTypeId id, UpdateEventTypeCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        ).Produces<EventTypeResponse>();

        group.MapDelete("/{id}", async Task<ApiResult> (EventTypeId id, IMediator mediator)
            => await mediator.Send(new DeleteEventTypeCommand(id))
        );
    }
}

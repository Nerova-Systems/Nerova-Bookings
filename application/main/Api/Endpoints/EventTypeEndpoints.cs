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

        group.MapPost("/{id}/image", async Task<ApiResult> (EventTypeId id, IFormFile file, IMediator mediator)
            => await mediator.Send(new UpdateEventTypeImageCommand(file.OpenReadStream(), file.ContentType) { Id = id })
        ).DisableAntiforgery();

        group.MapDelete("/{id}/image", async Task<ApiResult> (EventTypeId id, IMediator mediator)
            => await mediator.Send(new RemoveEventTypeImageCommand(id))
        );

        group.MapGet("/{id}/hashed-links", async Task<ApiResult<HashedLinksResponse>> (EventTypeId id, IMediator mediator)
            => await mediator.Send(new ListHashedLinksQuery(id))
        ).Produces<HashedLinksResponse>();

        group.MapPost("/{id}/hashed-links", async Task<ApiResult<HashedLinkResponse>> (EventTypeId id, CreateHashedLinkCommand command, IMediator mediator)
            => await mediator.Send(command with { EventTypeId = id })
        ).Produces<HashedLinkResponse>();

        group.MapDelete("/{id}/hashed-links/{hashedLinkId}", async Task<ApiResult> (EventTypeId id, HashedLinkId hashedLinkId, IMediator mediator)
            => await mediator.Send(new DeleteHashedLinkCommand(id, hashedLinkId))
        );

        group.MapPost("/{id}/team-assignment", async Task<ApiResult<EventTypeResponse>> (EventTypeId id, UpdateTeamAssignmentCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        ).Produces<EventTypeResponse>();

        group.MapGet("/by-viewer", async Task<ApiResult<EventTypesByViewerResponse>> (IMediator mediator)
            => await mediator.Send(new GetEventTypesByViewerQuery())
        ).Produces<EventTypesByViewerResponse>();

        group.MapGet("/groups", async Task<ApiResult<EventTypeGroupsResponse>> (IMediator mediator)
            => await mediator.Send(new GetEventTypeGroupsQuery())
        ).Produces<EventTypeGroupsResponse>();

        group.MapGet("/{id}/assignment-candidates", async Task<ApiResult<HostsForAssignmentResponse>> (EventTypeId id, IMediator mediator)
            => await mediator.Send(new GetHostsForAssignmentQuery(id))
        ).Produces<HostsForAssignmentResponse>();

        group.MapGet("/{id}/availability", async Task<ApiResult<HostsForAvailabilityResponse>> (EventTypeId id, DateOnly from, DateOnly to, IMediator mediator)
            => await mediator.Send(new GetHostsForAvailabilityQuery(id, from, to))
        ).Produces<HostsForAvailabilityResponse>();

        group.MapPost("/bulk-apply-locations", async Task<ApiResult<BulkApplyLocationsResponse>> (BulkApplyLocationsCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<BulkApplyLocationsResponse>();
    }
}

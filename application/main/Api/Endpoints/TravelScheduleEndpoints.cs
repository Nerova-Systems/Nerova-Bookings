using Main.Features.Schedules.Commands;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Domain;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class TravelScheduleEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/users/{userId}/travel-schedules";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("TravelSchedules").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<TravelSchedulesResponse>> (UserId userId, IMediator mediator)
            => await mediator.Send(new GetTravelSchedulesQuery(userId))
        ).Produces<TravelSchedulesResponse>();

        group.MapPost("/", async Task<ApiResult<TravelScheduleResponse>> (UserId userId, CreateTravelScheduleCommand command, IMediator mediator)
            => await mediator.Send(command with { UserId = userId })
        ).Produces<TravelScheduleResponse>();

        group.MapPut("/{id}", async Task<ApiResult<TravelScheduleResponse>> (UserId userId, TravelScheduleId id, UpdateTravelScheduleCommand command, IMediator mediator)
            => await mediator.Send(command with { UserId = userId, Id = id })
        ).Produces<TravelScheduleResponse>();

        group.MapDelete("/{id}", async Task<ApiResult> (UserId userId, TravelScheduleId id, IMediator mediator)
            => await mediator.Send(new DeleteTravelScheduleCommand(userId, id))
        );
    }
}

using Main.Features.Schedules.Commands;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Queries;
using Main.Features.Schedules.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class ScheduleEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/schedules";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Schedules").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<SchedulesResponse>> (IMediator mediator)
            => await mediator.Send(new GetSchedulesQuery())
        ).Produces<SchedulesResponse>();

        group.MapGet("/default", async Task<ApiResult<ScheduleResponse>> (IMediator mediator)
            => await mediator.Send(new GetDefaultScheduleQuery())
        ).Produces<ScheduleResponse>();

        group.MapGet("/{id}", async Task<ApiResult<ScheduleResponse>> (ScheduleId id, IMediator mediator)
            => await mediator.Send(new GetScheduleQuery(id))
        ).Produces<ScheduleResponse>();

        group.MapPost("/", async Task<ApiResult<ScheduleResponse>> (CreateScheduleCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<ScheduleResponse>();

        group.MapPost("/{id}/duplicate", async Task<ApiResult<ScheduleResponse>> (ScheduleId id, DuplicateScheduleRequest request, IMediator mediator)
            => await mediator.Send(new DuplicateScheduleCommand(id, request.Name))
        ).Produces<ScheduleResponse>();

        group.MapPut("/{id}", async Task<ApiResult<ScheduleResponse>> (ScheduleId id, UpdateScheduleCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        ).Produces<ScheduleResponse>();

        group.MapDelete("/{id}", async Task<ApiResult> (ScheduleId id, IMediator mediator)
            => await mediator.Send(new DeleteScheduleCommand(id))
        );
    }
}

using Main.Features.Schedules.Commands;
using Main.Features.Schedules.Domain;
using Main.Features.Schedules.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Domain;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class OutOfOfficeEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/users/{userId}/out-of-office";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("OutOfOffice").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<OutOfOfficesResponse>> (UserId userId, IMediator mediator)
            => await mediator.Send(new GetOutOfOfficesQuery(userId))
        ).Produces<OutOfOfficesResponse>();

        group.MapPost("/", async Task<ApiResult<OutOfOfficeResponse>> (UserId userId, CreateOutOfOfficeCommand command, IMediator mediator)
            => await mediator.Send(command with { UserId = userId })
        ).Produces<OutOfOfficeResponse>();

        group.MapPut("/{id}", async Task<ApiResult<OutOfOfficeResponse>> (UserId userId, OutOfOfficeId id, UpdateOutOfOfficeCommand command, IMediator mediator)
            => await mediator.Send(command with { UserId = userId, Id = id })
        ).Produces<OutOfOfficeResponse>();

        group.MapDelete("/{id}", async Task<ApiResult> (UserId userId, OutOfOfficeId id, IMediator mediator)
            => await mediator.Send(new DeleteOutOfOfficeCommand(userId, id))
        );
    }
}

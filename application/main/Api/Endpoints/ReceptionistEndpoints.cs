using Main.Features.Receptionist.Commands;
using Main.Features.Receptionist.Domain;
using Main.Features.Receptionist.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class ReceptionistEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/receptionist";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Receptionist").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/settings", async Task<ApiResult<ReceptionistSettingsResponse>> ([AsParameters] GetReceptionistSettingsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<ReceptionistSettingsResponse>();

        group.MapPut("/settings", async Task<ApiResult> (UpdateReceptionistSettingsCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapGet("/escalations", async Task<ApiResult<GetEscalationsResponse>> ([AsParameters] GetEscalationsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetEscalationsResponse>();

        group.MapPost("/escalations/{id}/resolve", async Task<ApiResult> (EscalationId id, ResolveEscalationCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );
    }
}

using Main.Features.Clients.Commands;
using Main.Features.Clients.Domain;
using Main.Features.Clients.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

/// <summary>
///     HTTP surface for the tenant Clients directory: list/search, fetch by id, update, delete, and
///     bulk delete. All routes are tenant-scoped via the authenticated user's tenant.
/// </summary>
public sealed class ClientEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/clients";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Clients").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<ClientsResponse>> ([AsParameters] GetClientsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<ClientsResponse>();

        group.MapGet("/{id}", async Task<ApiResult<ClientDetails>> (ClientId id, IMediator mediator)
            => await mediator.Send(new GetClientByIdQuery(id))
        ).Produces<ClientDetails>();

        group.MapPut("/{id}", async Task<ApiResult> (ClientId id, UpdateClientCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapDelete("/{id}", async Task<ApiResult> (ClientId id, IMediator mediator)
            => await mediator.Send(new DeleteClientCommand(id))
        );

        group.MapPost("/bulk-delete", async Task<ApiResult> (BulkDeleteClientsCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapGet("/{id}/vertical-fields", async Task<ApiResult<ClientVerticalFieldsResponse>> (ClientId id, IMediator mediator)
            => await mediator.Send(new GetClientVerticalFieldsQuery(id))
        ).Produces<ClientVerticalFieldsResponse>();

        group.MapPut("/{id}/vertical-fields", async Task<ApiResult> (ClientId id, UpdateClientVerticalFieldsCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );

        group.MapGet("/{id}/sensitive-fields", async Task<ApiResult<ClientSensitiveFieldsResponse>> (ClientId id, IMediator mediator)
            => await mediator.Send(new GetClientSensitiveFieldsQuery(id))
        ).Produces<ClientSensitiveFieldsResponse>();

        group.MapPut("/{id}/sensitive-fields", async Task<ApiResult> (ClientId id, UpdateClientSensitiveFieldsCommand command, IMediator mediator)
            => await mediator.Send(command with { Id = id })
        );
    }
}

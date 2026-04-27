using BackOffice.Features.Tenants.Commands;
using BackOffice.Features.Tenants.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Authorization;
using SharedKernel.Domain;
using SharedKernel.Endpoints;

namespace BackOffice.Api.Endpoints;

public sealed class TenantEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/back-office/tenants";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Tenants").RequireAuthorization(SysOpAuthorization.PolicyName).ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<TenantsResponse>> ([AsParameters] GetTenantsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<TenantsResponse>();

        group.MapGet("/{id}", async Task<ApiResult<TenantDetails>> (TenantId id, IMediator mediator)
            => await mediator.Send(new GetTenantByIdQuery(id))
        ).Produces<TenantDetails>();

        group.MapPost("/{id}/restore", async Task<ApiResult> (TenantId id, IMediator mediator)
            => await mediator.Send(new RestoreTenantCommand(id))
        );
    }
}

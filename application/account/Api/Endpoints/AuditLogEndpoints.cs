using Account.Features.AuditLog.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class AuditLogEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/audit-log";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(RoutesPrefix)
            .WithTags("AuditLog")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<AuditLogResponse>> ([AsParameters] GetAuditLogQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<AuditLogResponse>();
    }
}

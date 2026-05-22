using Account.Features.Smtp.Commands;
using Account.Features.Smtp.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class OrgSmtpEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/org/smtp";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(RoutesPrefix)
            .WithTags("OrgSmtp")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<OrgSmtpConfigResponse>> (IMediator mediator)
            => await mediator.Send(new GetOrgSmtpConfigQuery())
        ).Produces<OrgSmtpConfigResponse>();

        group.MapPut("/", async Task<ApiResult> (ConfigureOrgSmtpCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapDelete("/", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new RemoveOrgSmtpCommand())
        );

        group.MapPost("/test", async Task<ApiResult<TestOrgSmtpResult>> (TestOrgSmtpCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<TestOrgSmtpResult>();
    }
}

using Account.Features.SsoMicrosoft.Commands;
using Account.Features.SsoMicrosoft.Queries;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class MicrosoftSsoEndpoints : IEndpoints
{
    private const string MgmtRoutesPrefix = "/api/account/org/sso/microsoft";
    private const string FlowRoutesPrefix = "/api/account/sso/microsoft";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // ── Management endpoints (authenticated) ──────────────────────────────
        var mgmt = routes
            .MapGroup(MgmtRoutesPrefix)
            .WithTags("MicrosoftSso")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        mgmt.MapGet("/", async Task<ApiResult<OrgMicrosoftSsoConfigResponse>> (IMediator mediator)
            => await mediator.Send(new GetOrgMicrosoftSsoConfigQuery())
        ).Produces<OrgMicrosoftSsoConfigResponse>();

        mgmt.MapPut("/", async Task<ApiResult> (ConfigureMicrosoftSsoCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        mgmt.MapPost("/enable", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new EnableMicrosoftSsoCommand())
        );

        mgmt.MapPost("/disable", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new DisableMicrosoftSsoCommand())
        );

        mgmt.MapPost("/test", async Task<ApiResult<TestMicrosoftSsoResult>> (IMediator mediator)
            => await mediator.Send(new TestMicrosoftSsoCommand())
        ).Produces<TestMicrosoftSsoResult>();

        // ── SSO flow endpoints (anonymous) ────────────────────────────────────
        var flow = routes
            .MapGroup(FlowRoutesPrefix)
            .WithTags("MicrosoftSso")
            .WithGroupName(OpenApiDocumentNames.Account)
            .AllowAnonymous();

        flow.MapGet("/initiate", async Task<ApiResult<string>> (string org, IMediator mediator)
            => await mediator.Send(new StartMicrosoftSsoCommand(org))
        );

        flow.MapGet("/callback", async Task<ApiResult<string>> (
                string? code,
                string? state,
                string? error,
                [FromQuery(Name = "error_description")]
                string? errorDescription,
                IMediator mediator)
            => await mediator.Send(new CompleteMicrosoftSsoCommand(code, state, error, errorDescription))
        );
    }
}

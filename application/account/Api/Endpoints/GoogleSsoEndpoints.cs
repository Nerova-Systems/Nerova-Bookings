using Account.Features.SsoGoogle.Commands;
using Account.Features.SsoGoogle.Queries;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class GoogleSsoEndpoints : IEndpoints
{
    private const string MgmtRoutesPrefix = "/api/account/org/sso/google";
    private const string FlowRoutesPrefix = "/api/account/sso/google";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // ── Management endpoints (authenticated) ──────────────────────────────
        var mgmt = routes
            .MapGroup(MgmtRoutesPrefix)
            .WithTags("GoogleSso")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        mgmt.MapGet("/", async Task<ApiResult<OrgGoogleSsoConfigResponse>> (IMediator mediator)
            => await mediator.Send(new GetOrgGoogleSsoConfigQuery())
        ).Produces<OrgGoogleSsoConfigResponse>();

        mgmt.MapPut("/", async Task<ApiResult> (ConfigureGoogleSsoCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        mgmt.MapPost("/enable", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new EnableGoogleSsoCommand())
        );

        mgmt.MapPost("/disable", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new DisableGoogleSsoCommand())
        );

        mgmt.MapPost("/test", async Task<ApiResult<TestGoogleSsoResult>> (IMediator mediator)
            => await mediator.Send(new TestGoogleSsoCommand())
        ).Produces<TestGoogleSsoResult>();

        // ── SSO flow endpoints (anonymous) ────────────────────────────────────
        var flow = routes
            .MapGroup(FlowRoutesPrefix)
            .WithTags("GoogleSso")
            .WithGroupName(OpenApiDocumentNames.Account)
            .AllowAnonymous();

        flow.MapGet("/initiate", async Task<ApiResult<string>> (string org, IMediator mediator)
            => await mediator.Send(new StartGoogleSsoCommand(org))
        );

        flow.MapGet("/callback", async Task<ApiResult<string>> (
                string? code,
                string? state,
                string? error,
                [FromQuery(Name = "error_description")]
                string? errorDescription,
                IMediator mediator)
            => await mediator.Send(new CompleteGoogleSsoCommand(code, state, error, errorDescription))
        );
    }
}

using Main.Features.Apps.Commands;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Queries;
using Main.Features.Apps.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

/// <summary>
///     HTTP surface for the App platform. List, install (start OAuth), complete OAuth callback,
///     and uninstall. Connector tracks add new <c>App</c> registry rows and register installers;
///     they do not need to extend these endpoints.
/// </summary>
public sealed class AppEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/apps";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Apps").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<AppsResponse>> (IMediator mediator)
            => await mediator.Send(new ListAppsQuery())
        ).Produces<AppsResponse>();

        group.MapPost("/{slug}/install", async Task<ApiResult<InstallAppResponse>> (AppSlug slug, IMediator mediator)
            => await mediator.Send(new InstallAppCommand(slug))
        ).Produces<InstallAppResponse>();

        // Callback is hit by the OAuth provider — the user is still authenticated via the cookie,
        // so RequireAuthorization() applies. The state token provides CSRF protection.
        group.MapGet("/{slug}/callback", async Task<ApiResult<AppCallbackResponse>> (
                AppSlug slug, string code, string state, IMediator mediator)
            => await mediator.Send(new CompleteAppInstallCommand(slug, code, state))
        ).Produces<AppCallbackResponse>();

        group.MapDelete("/{slug}/uninstall", async Task<ApiResult> (AppSlug slug, IMediator mediator)
            => await mediator.Send(new UninstallAppCommand(slug))
        );
    }
}

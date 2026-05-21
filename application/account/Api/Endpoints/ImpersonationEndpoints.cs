using Account.Features.Impersonation.Commands;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class ImpersonationEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/impersonation";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix)
            .WithTags("Impersonation")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        group.MapPost("/start", async Task<ApiResult> (StartImpersonationCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapPost("/end", async Task<ApiResult> (IMediator mediator)
            => await mediator.Send(new EndImpersonationCommand())
        );
    }
}

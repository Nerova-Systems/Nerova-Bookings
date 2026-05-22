using Account.Features.DelegationCredentials.Commands.ConfigureDelegationCredential;
using Account.Features.DelegationCredentials.Commands.DeleteDelegationCredential;
using Account.Features.DelegationCredentials.Commands.DisableDelegationCredential;
using Account.Features.DelegationCredentials.Commands.EnableDelegationCredential;
using Account.Features.DelegationCredentials.Commands.TestDelegationCredential;
using Account.Features.DelegationCredentials.Queries.GetDelegationCredentials;
using SharedKernel.ApiResults;
using SharedKernel.DelegationCredentials;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class DelegationCredentialEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/org/delegation-credentials";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(RoutesPrefix)
            .WithTags("DelegationCredentials")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<DelegationCredentialResponse[]>> (IMediator mediator)
            => await mediator.Send(new GetDelegationCredentialsQuery())
        ).Produces<DelegationCredentialResponse[]>();

        group.MapPut("/", async Task<ApiResult> (ConfigureDelegationCredentialCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapPost("/{platform}/test", async Task<ApiResult<TestDelegationCredentialResult>> (
                WorkspacePlatform platform,
                TestDelegationCredentialCommandBody body,
                IMediator mediator)
            => await mediator.Send(new TestDelegationCredentialCommand { Platform = platform, MemberEmail = body.MemberEmail })
        ).Produces<TestDelegationCredentialResult>();

        group.MapPut("/{platform}/enable", async Task<ApiResult> (WorkspacePlatform platform, IMediator mediator)
            => await mediator.Send(new EnableDelegationCredentialCommand { Platform = platform })
        );

        group.MapPut("/{platform}/disable", async Task<ApiResult> (WorkspacePlatform platform, IMediator mediator)
            => await mediator.Send(new DisableDelegationCredentialCommand { Platform = platform })
        );

        group.MapDelete("/{platform}", async Task<ApiResult> (WorkspacePlatform platform, IMediator mediator)
            => await mediator.Send(new DeleteDelegationCredentialCommand { Platform = platform })
        );
    }
}

/// <summary>Request body for the test endpoint.</summary>
public sealed record TestDelegationCredentialCommandBody(string MemberEmail);

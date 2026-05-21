using Account.Features.ApiKeys.Commands.CreateOrgApiKey;
using Account.Features.ApiKeys.Commands.CreateUserApiKey;
using Account.Features.ApiKeys.Commands.RevokeOrgApiKey;
using Account.Features.ApiKeys.Commands.RevokeUserApiKey;
using Account.Features.ApiKeys.Domain;
using Account.Features.ApiKeys.Queries.ListOrgApiKeys;
using Account.Features.ApiKeys.Queries.ListUserApiKeys;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class ApiKeyEndpoints : IEndpoints
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // ─── User-scope keys ──────────────────────────────────────────────────
        var userGroup = routes
            .MapGroup("/api/account/api-keys")
            .WithTags("ApiKeys")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        userGroup.MapPost("/", async Task<ApiResult<CreateApiKeyResponse>> (CreateUserApiKeyCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<CreateApiKeyResponse>();

        userGroup.MapGet("/", async Task<ApiResult<IReadOnlyList<ApiKeyResponse>>> (IMediator mediator)
            => await mediator.Send(new ListUserApiKeysQuery())
        ).Produces<IReadOnlyList<ApiKeyResponse>>();

        userGroup.MapDelete("/{id}", async Task<ApiResult> (ApiKeyId id, IMediator mediator)
            => await mediator.Send(new RevokeUserApiKeyCommand(id))
        );

        // ─── Organisation-scope keys ──────────────────────────────────────────
        var orgGroup = routes
            .MapGroup("/api/account/org/api-keys")
            .WithTags("ApiKeys")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        orgGroup.MapPost("/", async Task<ApiResult<CreateApiKeyResponse>> (CreateOrgApiKeyCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<CreateApiKeyResponse>();

        orgGroup.MapGet("/", async Task<ApiResult<IReadOnlyList<ApiKeyResponse>>> (IMediator mediator)
            => await mediator.Send(new ListOrgApiKeysQuery())
        ).Produces<IReadOnlyList<ApiKeyResponse>>();

        orgGroup.MapDelete("/{id}", async Task<ApiResult> (ApiKeyId id, IMediator mediator)
            => await mediator.Send(new RevokeOrgApiKeyCommand(id))
        );
    }
}

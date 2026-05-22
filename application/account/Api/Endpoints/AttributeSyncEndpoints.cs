using Account.Features.AttributeSync;
using Account.Features.AttributeSync.Commands.ApplyAttributeSync;
using Account.Features.AttributeSync.Commands.CreateAttributeSyncRule;
using Account.Features.AttributeSync.Commands.DeleteAttributeSyncRule;
using Account.Features.AttributeSync.Commands.UpdateAttributeSyncRule;
using Account.Features.AttributeSync.Domain;
using Account.Features.AttributeSync.Queries.ListAttributeSyncRules;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class AttributeSyncEndpoints : IEndpoints
{
    private const string RoutePrefix = "/api/account/org/attribute-sync";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup(RoutePrefix)
            .WithTags("AttributeSync")
            .WithGroupName(OpenApiDocumentNames.Account)
            .RequireAuthorization()
            .ProducesValidationProblem();

        group.MapGet("/rules", async Task<ApiResult<AttributeSyncRuleResponse[]>> (IMediator mediator)
            => await mediator.Send(new ListAttributeSyncRulesQuery())
        ).Produces<AttributeSyncRuleResponse[]>();

        group.MapPost("/rules", async Task<ApiResult<AttributeSyncRuleResponse>> (CreateAttributeSyncRuleCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<AttributeSyncRuleResponse>();

        group.MapPut("/rules/{id}", async Task<ApiResult<AttributeSyncRuleResponse>> (
                AttributeSyncRuleId id,
                UpdateAttributeSyncRuleCommand command,
                IMediator mediator)
            => await mediator.Send(command with { RuleId = id })
        ).Produces<AttributeSyncRuleResponse>();

        group.MapDelete("/rules/{id}", async Task<ApiResult> (AttributeSyncRuleId id, IMediator mediator)
            => await mediator.Send(new DeleteAttributeSyncRuleCommand { RuleId = id })
        );

        group.MapPost("/apply", async Task<ApiResult> (ApplyAttributeSyncCommand command, IMediator mediator)
            => await mediator.Send(command)
        );
    }
}

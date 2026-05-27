using Main.Features.WhatsAppFlows.Commands;
using Main.Features.WhatsAppFlows.Queries;
using Main.Features.WhatsAppFlows.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class WhatsAppFlowsEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/whatsapp-flows";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("WhatsAppFlows").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/config", async Task<ApiResult<TenantFlowConfigResponse>> (IMediator mediator)
            => await mediator.Send(new GetTenantFlowConfigQuery())
        ).Produces<TenantFlowConfigResponse>();

        group.MapPost("/config", async Task<ApiResult<TenantFlowConfigResponse>> (SubmitQuestionnaireCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<TenantFlowConfigResponse>();

        group.MapPost("/config/questions", async Task<ApiResult<CustomQuestionResponse>> (AddCustomQuestionCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<CustomQuestionResponse>();

        group.MapDelete("/config/questions/{order:int}", async Task<ApiResult> (int order, IMediator mediator)
            => await mediator.Send(new RemoveCustomQuestionCommand(order))
        );

        group.MapPost("/publish", async Task<ApiResult<PublishFlowResponse>> (PublishFlowCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<PublishFlowResponse>();
    }
}

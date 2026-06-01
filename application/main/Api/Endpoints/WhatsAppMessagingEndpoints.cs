using Main.Features.WhatsAppMessaging.Commands;
using Main.Features.WhatsAppMessaging.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class WhatsAppMessagingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/whatsapp/messages";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("WhatsAppMessaging").RequireAuthorization().ProducesValidationProblem();

        group.MapPost("/", async Task<ApiResult<SendWhatsAppMessageResponse>> (SendWhatsAppMessageCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<SendWhatsAppMessageResponse>();

        group.MapGet("/", async Task<ApiResult<GetWhatsAppMessagesResponse>> ([AsParameters] GetWhatsAppMessagesQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetWhatsAppMessagesResponse>();
    }
}

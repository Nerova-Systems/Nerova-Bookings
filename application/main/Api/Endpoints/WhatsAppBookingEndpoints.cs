using Main.Features.WhatsAppBooking.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class WhatsAppBookingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/whatsapp/conversations";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("WhatsAppBooking").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<GetWhatsAppConversationsResponse>> ([AsParameters] GetWhatsAppConversationsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetWhatsAppConversationsResponse>();
    }
}

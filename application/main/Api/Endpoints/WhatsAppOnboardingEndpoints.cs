using Main.Features.WhatsAppOnboarding.Commands;
using Main.Features.WhatsAppOnboarding.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class WhatsAppOnboardingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/whatsapp";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("WhatsAppOnboarding").RequireAuthorization().ProducesValidationProblem();

        group.MapPost("/embedded-signup/complete", async Task<ApiResult> (CompleteEmbeddedSignupCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapGet("/status", async Task<ApiResult<GetWhatsAppOnboardingStatusResponse>> ([AsParameters] GetWhatsAppOnboardingStatusQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetWhatsAppOnboardingStatusResponse>();
    }
}

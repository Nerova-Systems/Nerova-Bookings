using Account.Features.Payments.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class PaymentsEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/payments";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Payments").WithGroupName(OpenApiDocumentNames.Account).RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/banks", async Task<ApiResult<GetPaystackBanksResponse>> ([AsParameters] GetPaystackBanksQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetPaystackBanksResponse>();
    }
}

using Account.Features.Billing.Queries;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Account.Api.Endpoints;

public sealed class BillingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/billing";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Billing").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/payment-history", async Task<ApiResult<PaymentHistoryResponse>> ([AsParameters] GetPaymentHistoryQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<PaymentHistoryResponse>();
    }
}

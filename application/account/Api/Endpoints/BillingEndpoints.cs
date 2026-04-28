using Account.Database;
using Account.Features.Billing.Commands;
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

        group.MapPut("/billing-info", async Task<ApiResult> (UpdateBillingInfoCommand command, IMediator mediator)
            => await mediator.Send(command)
        );

        group.MapPost("/start-payment-method-setup", async Task<ApiResult<StartPaymentMethodSetupResponse>> (IMediator mediator)
            => await mediator.Send(new StartPaymentMethodSetupCommand())
        ).Produces<StartPaymentMethodSetupResponse>();

        group.MapPost("/retry-pending-invoice", async Task<ApiResult<RetryPendingInvoicePaymentResponse>> (IMediator mediator, AccountDbContext dbContext, ILogger<BillingEndpoints> logger)
            => await BillingEndpointRetry.ExecuteAsync<RetryPendingInvoicePaymentResponse>(
                async () =>
                {
                    var result = await mediator.Send(new RetryPendingInvoicePaymentCommand());
                    return (ApiResult<RetryPendingInvoicePaymentResponse>)result;
                },
                dbContext,
                logger
            )
        ).Produces<RetryPendingInvoicePaymentResponse>();
    }
}

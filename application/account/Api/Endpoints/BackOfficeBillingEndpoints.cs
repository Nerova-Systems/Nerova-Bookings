using Account.Database;
using Account.Features.Billing.Commands;
using Account.Features.Billing.Queries;
using Account.Features.Subscriptions.Domain;
using SharedKernel.ApiResults;
using SharedKernel.Authorization;
using SharedKernel.Domain;
using SharedKernel.Endpoints;

namespace Account.Api.Endpoints;

public sealed class BackOfficeBillingEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/back-office/billing";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Back-office billing").RequireAuthorization(SysOpAuthorization.PolicyName).ProducesValidationProblem();

        group.MapGet("/tenants/{tenantId}", async Task<ApiResult<BackOfficeBillingTenantResponse>> (TenantId tenantId, IMediator mediator)
            => await mediator.Send(new GetBackOfficeBillingTenantQuery(tenantId))
        ).Produces<BackOfficeBillingTenantResponse>();

        group.MapPost("/tenants/{tenantId}/reconcile", async Task<ApiResult<ReconcileTenantBillingResponse>> (
                TenantId tenantId,
                IMediator mediator,
                AccountDbContext dbContext,
                ILogger<BackOfficeBillingEndpoints> logger
            )
                => await BillingEndpointRetry.ExecuteAsync<ReconcileTenantBillingResponse>(
                    async () =>
                    {
                        var result = await mediator.Send(new ReconcileTenantBillingCommand(tenantId));
                        return (ApiResult<ReconcileTenantBillingResponse>)result;
                    },
                    dbContext,
                    logger
                )
        ).Produces<ReconcileTenantBillingResponse>();

        group.MapPost("/transactions/{transactionId}/refund", async Task<ApiResult<RecordPaymentRefundResponse>> (
                PaymentTransactionId transactionId,
                RecordPaymentRefundRequest request,
                IMediator mediator,
                AccountDbContext dbContext,
                ILogger<BackOfficeBillingEndpoints> logger
            )
                => await BillingEndpointRetry.ExecuteAsync<RecordPaymentRefundResponse>(
                    async () =>
                    {
                        var result = await mediator.Send(new RecordPaymentRefundCommand(
                            transactionId,
                            request.Amount,
                            request.Reason,
                            request.CreditNoteUrl,
                            request.PayFastReference,
                            request.ProcessWithPayFast
                        ));
                        return (ApiResult<RecordPaymentRefundResponse>)result;
                    },
                    dbContext,
                    logger
                )
        ).Produces<RecordPaymentRefundResponse>();

        group.MapGet("/reconciliation-runs", async Task<ApiResult<BillingReconciliationRunsResponse>> ([AsParameters] GetBillingReconciliationRunsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BillingReconciliationRunsResponse>();
    }
}

public sealed record RecordPaymentRefundRequest(
    decimal Amount,
    string Reason,
    string? CreditNoteUrl,
    string? PayFastReference,
    bool ProcessWithPayFast = false
);

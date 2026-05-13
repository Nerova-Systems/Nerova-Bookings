using Account.Features.BackOffice.Invoices.Commands;
using Account.Features.BackOffice.Invoices.Queries;
using Account.Features.Subscriptions.Domain;
using Microsoft.Extensions.Options;
using SharedKernel.ApiResults;
using SharedKernel.Authentication.BackOfficeIdentity;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;

namespace Account.Api.BackOffice;

public sealed class InvoicesEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/back-office/invoices";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var backOfficeHost = routes.ServiceProvider.GetRequiredService<IOptions<BackOfficeHostOptions>>().Value.Host;

        var group = routes.MapGroup(RoutesPrefix)
            .WithTags("BackOfficeInvoices")
            .WithGroupName(OpenApiDocumentNames.BackOffice)
            .RequireHost(backOfficeHost)
            .RequireAuthorization(BackOfficeIdentityDefaults.PolicyName)
            .ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<BackOfficeInvoicesResponse>> ([AsParameters] GetBackOfficeInvoicesQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<BackOfficeInvoicesResponse>();

        group.MapPost("/{id}/refund", async Task<ApiResult> (PaymentTransactionId id, IMediator mediator)
            => await mediator.Send(new RefundBackOfficeInvoiceCommand(id))
        ).RequireAuthorization(BackOfficeIdentityDefaults.AdminPolicyName);
    }
}

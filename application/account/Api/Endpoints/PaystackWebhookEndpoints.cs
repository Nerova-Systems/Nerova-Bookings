using Account.Features.Subscriptions.Commands;
using Account.Features.Subscriptions.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using SharedKernel.OpenApi;
using Result = SharedKernel.Cqrs.Result;

namespace Account.Api.Endpoints;

public sealed class PaystackWebhookEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/subscriptions/paystack-webhook";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("PaystackWebhook").WithGroupName(OpenApiDocumentNames.Account).RequireAuthorization().ProducesValidationProblem();

        // Two-phase webhook processing with pessimistic locking requires inline logic beyond 3-line convention
        group.MapPost("/", async Task<ApiResult> (HttpRequest request, IMediator mediator, ProcessPendingPaystackEvents processPendingPaystackEvents) =>
            {
                var payload = await new StreamReader(request.Body).ReadToEndAsync();
                var signatureHeader = request.Headers["x-paystack-signature"].ToString();
                var acknowledgeResult = await mediator.Send(new AcknowledgePaystackWebhookCommand(payload, signatureHeader));
                if (!acknowledgeResult.IsSuccess) return Result.From(acknowledgeResult);

                var customerId = acknowledgeResult.Value;
                if (customerId is not null)
                {
                    await processPendingPaystackEvents.ExecuteAsync(customerId, request.HttpContext.RequestAborted);
                }

                return Result.Success();
            }
        ).AllowAnonymous().DisableAntiforgery();
    }
}

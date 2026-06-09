using Main.Features.Payments.Commands;
using Main.Features.Payments.Paystack;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using Result = SharedKernel.Cqrs.Result;

namespace Main.Api.Endpoints;

/// <summary>
///     Paystack webhook for booking-level payments (the post-flow Paystack link the
///     <c>main</c> SCS dispatches via WhatsApp). Anonymous because Paystack does not include a bearer
///     token; authenticity is established by the HMAC-SHA512 signature in <c>x-paystack-signature</c>.
///     Idempotent via the <c>processed_payment_events</c> table — Paystack retries until it gets a
///     2xx response.
/// </summary>
public sealed class PaystackPaymentWebhookEndpoint : IEndpoints
{
    private const string RoutePath = "/api/payments/paystack/webhook";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapPost(RoutePath, async Task<ApiResult> (
                HttpRequest request,
                IPaystackWebhookVerifier verifier,
                IMediator mediator,
                CancellationToken cancellationToken
            ) =>
            {
                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync(cancellationToken);

                var signature = request.Headers["x-paystack-signature"].ToString();
                if (string.IsNullOrWhiteSpace(signature))
                {
                    signature = request.Headers["Paystack-Signature"].ToString();
                }

                var verified = verifier.Verify(payload, signature);
                if (verified is null)
                {
                    return Result.From(Result.Unauthorized("Invalid Paystack webhook signature."));
                }

                // Only react to terminal charge events. Other events (e.g. transfer.success) are
                // out of scope for booking payments — accept + ignore so Paystack stops retrying.
                var dispatchResult = verified.EventType switch
                {
                    "charge.success" => await mediator.Send(new ConfirmBookingPaymentCommand(verified.Reference, verified.EventId), cancellationToken),
                    "charge.failed" => await mediator.Send(new ReleaseBookingPaymentCommand(verified.Reference, verified.EventId), cancellationToken),
                    _ => Result.Success()
                };

                return Result.From(dispatchResult);
            }
        ).WithTags("PaystackPaymentWebhook").AllowAnonymous().DisableAntiforgery();
    }
}

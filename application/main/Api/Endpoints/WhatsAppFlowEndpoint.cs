using Main.Features.WhatsAppFlows.Endpoint;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

/// <summary>
///     Public Meta-callable endpoint that powers a live WhatsApp Flow session. Meta encrypts the
///     payload with the tenant's public key; we decrypt with the matching private key and route to
///     a screen handler. Authenticity is established by the encryption itself — only this server
///     can decrypt — so the endpoint is anonymous.
///     <para>
///         TODO: Meta also sends an <c>X-Hub-Signature-256</c> HMAC header. We can additionally
///         verify it once the App Secret is provisioned per tenant.
///     </para>
/// </summary>
public sealed class WhatsAppFlowEndpoint : IEndpoints
{
    private const string RoutesPrefix = "/api/whatsapp/flows";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("WhatsAppFlows").AllowAnonymous();

        group.MapPost("/v1", async (
                EncryptedFlowRequest body,
                IWhatsAppFlowDispatcher dispatcher,
                HttpContext httpContext,
                CancellationToken cancellationToken
            ) =>
            {
                var phoneNumberId = httpContext.Request.Headers["X-WABA-Phone-Number-Id"].ToString();
                var outcome = await dispatcher.Dispatch(body, phoneNumberId, cancellationToken);
                return Results.Text(outcome.Body, "text/plain", statusCode: outcome.StatusCode);
            }
        );
    }
}

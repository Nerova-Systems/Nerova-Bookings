using Main.Features.WhatsAppMessaging.Commands;
using Main.Features.WhatsAppMessaging.Shared;
using Microsoft.Extensions.Configuration;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;
using Result = SharedKernel.Cqrs.Result;

namespace Main.Api.Endpoints;

public sealed class WhatsAppWebhookEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/main/whatsapp/webhook";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("WhatsAppWebhook").RequireAuthorization().ProducesValidationProblem();

        // Webhook verification: Meta sends a GET with hub.mode=subscribe and hub.verify_token to confirm the endpoint
        group.MapGet("/", (HttpRequest request, IConfiguration configuration) =>
        {
            var mode = request.Query["hub.mode"].ToString();
            var token = request.Query["hub.verify_token"].ToString();
            var challenge = request.Query["hub.challenge"].ToString();

            var configuredToken = configuration["Meta:WebhookVerifyToken"] ?? string.Empty;

            if (mode == "subscribe" && token == configuredToken)
            {
                return Results.Content(challenge, "text/plain");
            }

            return Results.Forbid();
        }).AllowAnonymous().DisableAntiforgery();

        // Two-phase webhook processing with pessimistic locking requires inline logic beyond 3-line convention
        group.MapPost("/", async Task<ApiResult> (HttpRequest request, IMediator mediator, ProcessPendingWhatsAppEvents processPendingWhatsAppEvents) =>
            {
                var payload = await new StreamReader(request.Body).ReadToEndAsync();
                if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeaderValues) || signatureHeaderValues.Count != 1)
                {
                    return Result.Unauthorized("X-Hub-Signature-256 header missing or duplicated.");
                }

                var signatureHeader = signatureHeaderValues[0]!;
                var acknowledgeResult = await mediator.Send(new AcknowledgeWhatsAppWebhookCommand(payload, signatureHeader));
                if (!acknowledgeResult.IsSuccess) return Result.From(acknowledgeResult);

                var whatsAppEvent = acknowledgeResult.Value;
                if (whatsAppEvent is not null)
                {
                    await processPendingWhatsAppEvents.ExecuteAsync(whatsAppEvent, request.HttpContext.RequestAborted);
                }

                return Result.Success();
            }
        ).AllowAnonymous().DisableAntiforgery();
    }
}

using Main.Features.WhatsAppMessaging.Commands;
using Main.Features.WhatsAppMessaging.Queries;
using Main.Features.WhatsAppMessaging.Shared;
using Main.Integrations.Meta;
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

        group.MapGet("/events", async Task<ApiResult<GetWhatsAppWebhookEventsResponse>> ([AsParameters] GetWhatsAppWebhookEventsQuery query, IMediator mediator)
            => await mediator.Send(query)
        ).Produces<GetWhatsAppWebhookEventsResponse>();

        group.MapGet("/diagnostics", (IConfiguration configuration, MetaGraphClientFactory metaGraphClientFactory) =>
            {
                var hasAppId = !string.IsNullOrWhiteSpace(configuration["Meta:AppId"]) && configuration["Meta:AppId"] != "not-configured";
                var hasAppSecret = !string.IsNullOrWhiteSpace(configuration["Meta:AppSecret"]) && configuration["Meta:AppSecret"] != "not-configured";
                var hasConfigId = !string.IsNullOrWhiteSpace(configuration["Meta:ConfigId"]) && configuration["Meta:ConfigId"] != "not-configured";
                var hasWebhookVerifyToken = !string.IsNullOrWhiteSpace(configuration["Meta:WebhookVerifyToken"]) && configuration["Meta:WebhookVerifyToken"] != "not-configured";

                return Results.Ok(new
                {
                    metaConfigured = metaGraphClientFactory.IsConfigured,
                    usesMockProvider = !metaGraphClientFactory.IsConfigured,
                    hasAppId,
                    hasAppSecret,
                    hasConfigId,
                    hasWebhookVerifyToken,
                    webhookPath = RoutesPrefix,
                    note = "This endpoint reports only whether the live deployment has real Meta credentials configured; it never exposes secret values."
                });
            })
            .AllowAnonymous();

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
            }
        ).AllowAnonymous().DisableAntiforgery();

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

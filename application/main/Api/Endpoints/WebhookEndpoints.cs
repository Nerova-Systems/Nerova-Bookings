using Main.Features.Webhooks.Commands;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Queries;
using Main.Features.Webhooks.Shared;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

/// <summary>
///     HTTP surface for the webhook platform. Booking-lifecycle wiring that emits events into
///     <see cref="Main.Features.Webhooks.Infrastructure.IWebhookDispatcher" /> is owned by track
///     T3-booking-webhooks; this track only exposes the CRUD + test-fire surface.
/// </summary>
public sealed class WebhookEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/webhooks";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Webhooks").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/", async Task<ApiResult<WebhooksResponse>> (IMediator mediator)
            => await mediator.Send(new ListWebhooksQuery())
        ).Produces<WebhooksResponse>();

        group.MapPost("/", async Task<ApiResult<WebhookResponse>> (CreateWebhookCommand command, IMediator mediator)
            => await mediator.Send(command)
        ).Produces<WebhookResponse>();

        group.MapPut("/{id}", async Task<ApiResult<WebhookResponse>> (WebhookId id, UpdateWebhookRequest body, IMediator mediator)
            => await mediator.Send(new UpdateWebhookCommand(id, body.TargetUrl, body.EventSubscriptions, body.Active))
        ).Produces<WebhookResponse>();

        group.MapDelete("/{id}", async Task<ApiResult> (WebhookId id, IMediator mediator)
            => await mediator.Send(new DeleteWebhookCommand(id))
        );

        group.MapPost("/{id}/test", async Task<ApiResult<TestWebhookResponse>> (WebhookId id, IMediator mediator)
            => await mediator.Send(new TestWebhookCommand(id))
        ).Produces<TestWebhookResponse>();
    }
}

/// <summary>Request body for PUT /api/webhooks/{id}; id comes from the route.</summary>
public sealed record UpdateWebhookRequest(string TargetUrl, WebhookEventType[] EventSubscriptions, bool Active);

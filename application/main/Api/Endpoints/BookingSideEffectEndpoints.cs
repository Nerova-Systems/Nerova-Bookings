using Main.Features.BookingSideEffects.Commands;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.BookingSideEffects.Queries;
using Main.Features.BookingSideEffects.Shared;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.ApiResults;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class BookingSideEffectEndpoints : IEndpoints
{
    private const string EventTypeRoutesPrefix = "/api/event-types/{eventTypeId}";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(EventTypeRoutesPrefix).WithTags("BookingSideEffects").RequireAuthorization().ProducesValidationProblem();

        group.MapGet("/workflows", async Task<ApiResult<WorkflowsResponse>> (EventTypeId eventTypeId, IMediator mediator)
            => await mediator.Send(new GetWorkflowsQuery(eventTypeId))
        ).Produces<WorkflowsResponse>();

        group.MapPost("/workflows", async Task<ApiResult<WorkflowResponse>> (EventTypeId eventTypeId, CreateWorkflowRequest request, IMediator mediator)
            => await mediator.Send(new CreateWorkflowCommand(eventTypeId, request.Name, request.Active, request.Trigger, request.ScheduledOffsetMinutes, request.Steps))
        ).Produces<WorkflowResponse>();

        group.MapPut("/workflows/{id}", async Task<ApiResult<WorkflowResponse>> (EventTypeId eventTypeId, WorkflowId id, UpdateWorkflowRequest request, IMediator mediator)
            => await mediator.Send(new UpdateWorkflowCommand(eventTypeId, id, request.Name, request.Active, request.Trigger, request.ScheduledOffsetMinutes, request.Steps))
        ).Produces<WorkflowResponse>();

        group.MapDelete("/workflows/{id}", async Task<ApiResult> (EventTypeId eventTypeId, WorkflowId id, IMediator mediator)
            => await mediator.Send(new DeleteWorkflowCommand(eventTypeId, id))
        );

        group.MapGet("/webhooks", async Task<ApiResult<WebhookSubscriptionsResponse>> (EventTypeId eventTypeId, IMediator mediator)
            => await mediator.Send(new GetWebhookSubscriptionsQuery(eventTypeId))
        ).Produces<WebhookSubscriptionsResponse>();

        group.MapPost("/webhooks", async Task<ApiResult<WebhookSubscriptionResponse>> (EventTypeId eventTypeId, CreateWebhookSubscriptionRequest request, IMediator mediator)
            => await mediator.Send(new CreateWebhookSubscriptionCommand(eventTypeId, request.Active, request.SubscriberUrl, request.Secret, request.Triggers, request.PayloadFormat, request.PayloadVersion))
        ).Produces<WebhookSubscriptionResponse>();

        group.MapPut("/webhooks/{id}", async Task<ApiResult<WebhookSubscriptionResponse>> (EventTypeId eventTypeId, WebhookSubscriptionId id, UpdateWebhookSubscriptionRequest request, IMediator mediator)
            => await mediator.Send(new UpdateWebhookSubscriptionCommand(eventTypeId, id, request.Active, request.SubscriberUrl, request.Secret, request.Triggers, request.PayloadFormat, request.PayloadVersion))
        ).Produces<WebhookSubscriptionResponse>();

        group.MapPost("/webhooks/{id}/test", async Task<ApiResult<BookingSideEffectDeliverySummaryResponse>> (EventTypeId eventTypeId, WebhookSubscriptionId id, IMediator mediator)
            => await mediator.Send(new TestWebhookSubscriptionCommand(eventTypeId, id))
        ).Produces<BookingSideEffectDeliverySummaryResponse>();

        group.MapDelete("/webhooks/{id}", async Task<ApiResult> (EventTypeId eventTypeId, WebhookSubscriptionId id, IMediator mediator)
            => await mediator.Send(new DeleteWebhookSubscriptionCommand(eventTypeId, id))
        );

        group.MapGet("/side-effect-deliveries", async Task<ApiResult<BookingSideEffectDeliveriesResponse>> (EventTypeId eventTypeId, IMediator mediator)
            => await mediator.Send(new GetEventTypeSideEffectDeliveriesQuery(eventTypeId))
        ).Produces<BookingSideEffectDeliveriesResponse>();

        routes.MapGroup("/api/bookings")
            .WithTags("BookingSideEffects")
            .RequireAuthorization()
            .ProducesValidationProblem()
            .MapGet("/{id}/side-effects", async Task<ApiResult<BookingSideEffectDeliveriesResponse>> (BookingId id, IMediator mediator)
                => await mediator.Send(new GetBookingSideEffectDeliveriesQuery(id))
            )
            .Produces<BookingSideEffectDeliveriesResponse>();
    }
}

public sealed record CreateWorkflowRequest(string Name, bool Active, string Trigger, int? ScheduledOffsetMinutes, WorkflowStep[] Steps);

public sealed record UpdateWorkflowRequest(string Name, bool Active, string Trigger, int? ScheduledOffsetMinutes, WorkflowStep[] Steps);

public sealed record CreateWebhookSubscriptionRequest(
    bool Active,
    string SubscriberUrl,
    string? Secret,
    string[] Triggers,
    string PayloadFormat = "cal-com",
    string PayloadVersion = "v1"
);

public sealed record UpdateWebhookSubscriptionRequest(
    bool Active,
    string SubscriberUrl,
    string? Secret,
    string[] Triggers,
    string PayloadFormat = "cal-com",
    string PayloadVersion = "v1"
);

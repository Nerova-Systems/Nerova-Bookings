using Main.Features.BookingSideEffects.Domain;

namespace Main.Features.BookingSideEffects.Shared;

public sealed record WorkflowsResponse(WorkflowResponse[] Workflows);

public sealed record WorkflowResponse(
    WorkflowId Id,
    string Name,
    bool Active,
    string Trigger,
    int? ScheduledOffsetMinutes,
    WorkflowStep[] Steps
)
{
    public static WorkflowResponse From(Workflow workflow)
    {
        return new WorkflowResponse(workflow.Id, workflow.Name, workflow.Active, workflow.Trigger, workflow.ScheduledOffsetMinutes, workflow.Steps);
    }
}

public sealed record WebhookSubscriptionsResponse(WebhookSubscriptionResponse[] Webhooks);

public sealed record WebhookSubscriptionResponse(
    WebhookSubscriptionId Id,
    bool Active,
    string SubscriberUrl,
    string? Secret,
    string[] Triggers,
    string PayloadFormat,
    string PayloadVersion
)
{
    public static WebhookSubscriptionResponse From(WebhookSubscription subscription)
    {
        return new WebhookSubscriptionResponse(subscription.Id, subscription.Active, subscription.SubscriberUrl, subscription.Secret, subscription.Triggers, subscription.PayloadFormat, subscription.PayloadVersion);
    }
}

public sealed record BookingSideEffectDeliveriesResponse(BookingSideEffectDeliverySummaryResponse[] Deliveries);

public sealed record BookingSideEffectDeliverySummaryResponse(
    string Id,
    string BookingId,
    string Trigger,
    string Kind,
    string Status,
    int Attempts,
    DateTimeOffset? NextRetryAt,
    string? LastError
)
{
    public static BookingSideEffectDeliverySummaryResponse From(BookingSideEffectDelivery delivery)
    {
        return new BookingSideEffectDeliverySummaryResponse(
            delivery.Id.Value,
            delivery.BookingId.Value,
            delivery.Trigger,
            delivery.Kind,
            delivery.Status,
            delivery.Attempts,
            delivery.NextRetryAt,
            delivery.LastError
        );
    }
}

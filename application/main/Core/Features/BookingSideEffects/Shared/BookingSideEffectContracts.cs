using System.Text.Json;
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
    string? Operation,
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
            ResolveOperation(delivery),
            delivery.Attempts,
            delivery.NextRetryAt,
            delivery.LastError
        );
    }

    private static string? ResolveOperation(BookingSideEffectDelivery delivery)
    {
        if (!delivery.Kind.Equals(BookingSideEffectConstants.CalendarKind, StringComparison.OrdinalIgnoreCase) &&
            !delivery.Kind.Equals(BookingSideEffectConstants.ConferencingKind, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var operation = ReadOperation(delivery.PayloadJson);
        if (!string.IsNullOrWhiteSpace(operation))
        {
            return operation.Trim().ToLowerInvariant();
        }

        return delivery.Trigger switch
        {
            BookingSideEffectConstants.BookingCreated => BookingSideEffectConstants.CreateOperation,
            BookingSideEffectConstants.BookingConfirmed => BookingSideEffectConstants.UpdateOperation,
            BookingSideEffectConstants.BookingLocationChanged => BookingSideEffectConstants.UpdateOperation,
            BookingSideEffectConstants.BookingGuestsAdded => BookingSideEffectConstants.UpdateOperation,
            BookingSideEffectConstants.BookingRejected => BookingSideEffectConstants.DeleteOperation,
            BookingSideEffectConstants.BookingCancelled => BookingSideEffectConstants.DeleteOperation,
            BookingSideEffectConstants.BookingRescheduled => BookingSideEffectConstants.DeleteOperation,
            _ => BookingSideEffectConstants.UpdateOperation
        };
    }

    private static string? ReadOperation(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.TryGetProperty(nameof(BookingConnectorCalendarDeliveryPayload.Operation), out var operationElement) &&
                   operationElement.ValueKind == JsonValueKind.String
                ? operationElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

using System.Text.Json;
using Main.Features.Connectors.Domain;
using Main.Features.EventTypes.Domain;

namespace Main.Features.BookingSideEffects.Domain;

public sealed class BookingSideEffectEnqueueHandler(
    IWorkflowRepository workflowRepository,
    IWebhookSubscriptionRepository webhookSubscriptionRepository,
    IEventTypeRepository eventTypeRepository,
    IBookingSideEffectDeliveryRepository deliveryRepository,
    TimeProvider timeProvider
) : INotificationHandler<BookingLifecycleSideEffectEvent>
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = JsonSerializerOptions.Default;

    public async Task Handle(BookingLifecycleSideEffectEvent notification, CancellationToken cancellationToken)
    {
        var workflows = await workflowRepository.GetActiveForEventTypeTriggerAsync(notification.TenantId, notification.EventTypeId, notification.Trigger, cancellationToken);
        foreach (var workflow in workflows)
        {
            await EnqueueWorkflowStepsAsync(notification, workflow, cancellationToken);
        }

        var webhooks = await webhookSubscriptionRepository.GetActiveForEventTypeTriggerAsync(notification.TenantId, notification.EventTypeId, notification.Trigger, cancellationToken);
        foreach (var webhook in webhooks)
        {
            await EnqueueWebhookAsync(notification, webhook, cancellationToken);
        }

        var eventType = await eventTypeRepository.GetByIdUnfilteredAsync(notification.TenantId, notification.EventTypeId, cancellationToken);
        if (eventType is not null)
        {
            await EnqueueConnectorSyncAsync(notification, eventType, cancellationToken);
        }
    }

    private async Task EnqueueWorkflowStepsAsync(BookingLifecycleSideEffectEvent notification, Workflow workflow, CancellationToken cancellationToken)
    {
        var steps = workflow.Steps;
        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            if (!step.Kind.Equals(BookingSideEffectConstants.EmailKind, StringComparison.OrdinalIgnoreCase)) continue;

            var dedupeKey = $"{notification.BookingId}:{notification.Trigger}:workflow:{workflow.Id}:{index}";
            if (await deliveryRepository.ExistsByDedupeKeyAsync(dedupeKey, cancellationToken)) continue;

            var payloadJson = JsonSerializer.Serialize(
                new BookingEmailDeliveryPayload(
                    notification.Trigger,
                    workflow.Id.Value,
                    step.Recipient,
                    step.Subject,
                    step.Body,
                    notification.Title,
                    notification.BookerName,
                    notification.BookerEmail,
                    notification.StartTime,
                    notification.EndTime,
                    notification.Status
                ),
                JsonSerializerOptions
            );
            await deliveryRepository.AddAsync(
                BookingSideEffectDelivery.Create(
                    notification.TenantId,
                    notification.BookingId,
                    notification.EventTypeId,
                    notification.Trigger,
                    BookingSideEffectConstants.EmailKind,
                    payloadJson,
                    dedupeKey,
                    timeProvider.GetUtcNow()
                ),
                cancellationToken
            );
        }
    }

    private async Task EnqueueWebhookAsync(BookingLifecycleSideEffectEvent notification, WebhookSubscription webhook, CancellationToken cancellationToken)
    {
        var dedupeKey = $"{notification.BookingId}:{notification.Trigger}:webhook:{webhook.Id}";
        if (await deliveryRepository.ExistsByDedupeKeyAsync(dedupeKey, cancellationToken)) return;

        var payloadJson = JsonSerializer.Serialize(
            new BookingWebhookDeliveryPayload(
                notification.Trigger,
                webhook.Id.Value,
                webhook.SubscriberUrl,
                webhook.Secret,
                webhook.PayloadFormat,
                webhook.PayloadVersion,
                notification.BookingId.Value,
                notification.EventTypeId.Value,
                notification.Title,
                notification.BookerName,
                notification.BookerEmail,
                notification.StartTime,
                notification.EndTime,
                notification.Status,
                notification.LocationType,
                notification.LocationValue
            ),
            JsonSerializerOptions
        );
        await deliveryRepository.AddAsync(
            BookingSideEffectDelivery.Create(
                notification.TenantId,
                notification.BookingId,
                notification.EventTypeId,
                notification.Trigger,
                BookingSideEffectConstants.WebhookKind,
                payloadJson,
                dedupeKey,
                timeProvider.GetUtcNow()
            ),
            cancellationToken
        );
    }

    private async Task EnqueueConnectorSyncAsync(BookingLifecycleSideEffectEvent notification, EventType eventType, CancellationToken cancellationToken)
    {
        if (eventType.Settings.DestinationCalendar is { } destinationCalendar &&
            CoreConnectorConstants.IsCoreCalendar(destinationCalendar.Integration))
        {
            await EnqueueDeliveryAsync(
                notification,
                BookingSideEffectConstants.CalendarKind,
                $"calendar:{destinationCalendar.Integration}:{destinationCalendar.ExternalId}",
                new BookingConnectorCalendarDeliveryPayload(
                    notification.Trigger,
                    destinationCalendar.Integration,
                    destinationCalendar.ExternalId,
                    destinationCalendar.CredentialId
                ),
                cancellationToken
            );
        }

        var conferencing = ResolveConferencing(eventType);
        if (conferencing is not null && CoreConnectorConstants.IsCoreConferencing(conferencing.App))
        {
            await EnqueueDeliveryAsync(
                notification,
                BookingSideEffectConstants.ConferencingKind,
                $"conferencing:{conferencing.App}",
                new BookingConnectorConferencingDeliveryPayload(notification.Trigger, conferencing.App, conferencing.CredentialId),
                cancellationToken
            );
        }
    }

    private async Task EnqueueDeliveryAsync<TPayload>(
        BookingLifecycleSideEffectEvent notification,
        string kind,
        string scope,
        TPayload payload,
        CancellationToken cancellationToken
    )
    {
        var dedupeKey = $"{notification.BookingId}:{notification.Trigger}:{scope}";
        if (await deliveryRepository.ExistsByDedupeKeyAsync(dedupeKey, cancellationToken)) return;

        await deliveryRepository.AddAsync(
            BookingSideEffectDelivery.Create(
                notification.TenantId,
                notification.BookingId,
                notification.EventTypeId,
                notification.Trigger,
                kind,
                JsonSerializer.Serialize(payload, JsonSerializerOptions),
                dedupeKey,
                timeProvider.GetUtcNow()
            ),
            cancellationToken
        );
    }

    private static EventTypeDefaultConferencing? ResolveConferencing(EventType eventType)
    {
        if (eventType.Settings.DefaultConferencing is { } defaultConferencing)
        {
            return defaultConferencing;
        }

        return eventType.LocationType is not null &&
               eventType.LocationType.Equals("integration", StringComparison.OrdinalIgnoreCase) &&
               eventType.LocationValue is not null &&
               CoreConnectorConstants.IsCoreConferencing(eventType.LocationValue)
            ? new EventTypeDefaultConferencing { App = eventType.LocationValue }
            : null;
    }
}

public sealed record BookingEmailDeliveryPayload(
    string Trigger,
    string WorkflowId,
    string Recipient,
    string? Subject,
    string? Body,
    string EventTitle,
    string BookerName,
    string BookerEmail,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status
);

public sealed record BookingWebhookDeliveryPayload(
    string Trigger,
    string WebhookSubscriptionId,
    string SubscriberUrl,
    string? Secret,
    string PayloadFormat,
    string PayloadVersion,
    string BookingId,
    string EventTypeId,
    string EventTitle,
    string BookerName,
    string BookerEmail,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status,
    string? LocationType,
    string? LocationValue
);

public sealed record BookingConnectorCalendarDeliveryPayload(
    string Trigger,
    string Integration,
    string ExternalId,
    string? CredentialId
);

public sealed record BookingConnectorConferencingDeliveryPayload(
    string Trigger,
    string App,
    string? CredentialId
);

using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Infrastructure;

namespace Main.Features.Scheduling.Notifications;

/// <summary>
///     Fans out booking-lifecycle webhooks. Thin wrapper around <see cref="IWebhookDispatcher" />
///     that owns the payload-shape contract for booking events: command handlers stay focused on
///     persistence, this service owns the wire format.
///     <para>
///         <b>Best-effort.</b> Failures are logged and swallowed — a flaky tenant endpoint must
///         not roll back a booking write. The fan-out itself only enqueues
///         <see cref="WebhookDelivery" /> rows; retries and HTTP delivery happen on the worker.
///     </para>
/// </summary>
[PublicAPI]
public interface IBookingWebhookNotifier
{
    Task NotifyAsync(
        WebhookEventType triggerEvent,
        Booking booking,
        EventType? eventType,
        IReadOnlyList<BookingAttendee>? attendees,
        BookingReport? report,
        CancellationToken cancellationToken
    );
}

public sealed class BookingWebhookNotifier(
    IWebhookDispatcher webhookDispatcher,
    TimeProvider timeProvider,
    ILogger<BookingWebhookNotifier> logger
) : IBookingWebhookNotifier
{
    public async Task NotifyAsync(
        WebhookEventType triggerEvent,
        Booking booking,
        EventType? eventType,
        IReadOnlyList<BookingAttendee>? attendees,
        BookingReport? report,
        CancellationToken cancellationToken
    )
    {
        var payloadJson = BookingWebhookPayloadBuilder.Build(
            triggerEvent,
            timeProvider.GetUtcNow(),
            booking,
            eventType,
            attendees,
            report
        );

        try
        {
            var deliveryIds = await webhookDispatcher.FanOutAsync(
                booking.TenantId,
                triggerEvent,
                payloadJson,
                cancellationToken
            );

            if (deliveryIds.Length > 0)
            {
                logger.LogInformation(
                    "Webhook {TriggerEvent} for booking {BookingId} fanned out to {Count} subscriber(s).",
                    triggerEvent, booking.Id, deliveryIds.Length
                );
            }
        }
        catch (Exception ex)
        {
            // Swallow: webhook fan-out is best-effort. A throw here would surface as a 500 on the
            // booking write — the booking is already persisted, so re-trying the whole command is
            // worse than losing the webhook. Operators see the failure in logs.
            logger.LogError(
                ex,
                "Webhook fan-out failed for {TriggerEvent} on booking {BookingId}.",
                triggerEvent, booking.Id
            );
        }
    }
}

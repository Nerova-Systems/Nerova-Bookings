using JetBrains.Annotations;

namespace Main.Features.Webhooks.Domain;

/// <summary>
///     The set of platform events a <see cref="Webhook" /> can subscribe to. Mirrors the subset of
///     cal.com <c>WebhookTriggerEvents</c> we initially expose. Booking-lifecycle wiring is owned
///     by track T3-booking-webhooks; this enum is shipped here so the platform contract is stable.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WebhookEventType
{
    BookingCreated,
    BookingCancelled,
    BookingRescheduled,
    BookingPaid,
    BookingReported,
    MeetingStarted,
    MeetingEnded,
    FormSubmitted,
    RecordingReady,

    /// <summary>Synthetic event emitted by the test-fire endpoint so users can validate their endpoint.</summary>
    Ping
}

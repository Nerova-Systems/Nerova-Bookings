namespace Main.Features.BookingSideEffects.Domain;

public static class BookingSideEffectConstants
{
    public const string BookingCreated = "BOOKING_CREATED";
    public const string BookingConfirmed = "BOOKING_CONFIRMED";
    public const string BookingRejected = "BOOKING_REJECTED";
    public const string BookingCancelled = "BOOKING_CANCELLED";
    public const string BookingRescheduled = "BOOKING_RESCHEDULED";
    public const string BookingLocationChanged = "BOOKING_LOCATION_CHANGED";
    public const string BookingGuestsAdded = "BOOKING_GUESTS_ADDED";

    public const string EmailKind = "email";
    public const string WebhookKind = "webhook";

    public const string PendingStatus = "pending";
    public const string SentStatus = "sent";
    public const string FailedStatus = "failed";

    public static readonly string[] SupportedTriggers =
    [
        BookingCreated,
        BookingConfirmed,
        BookingRejected,
        BookingCancelled,
        BookingRescheduled,
        BookingLocationChanged,
        BookingGuestsAdded
    ];
}

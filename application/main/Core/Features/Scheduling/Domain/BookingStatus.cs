using JetBrains.Annotations;

namespace Main.Features.Scheduling.Domain;

/// <summary>
///     Lifecycle status of a <see cref="Booking" />. Mirrors the cal.com
///     <c>BookingStatus</c> enum (ACCEPTED, PENDING, AWAITING_HOST, CANCELLED, REJECTED).
///     Persisted as the enum name string via the default EF Core convention.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingStatus
{
    /// <summary>The booking has been confirmed by the host (or no confirmation required).</summary>
    Accepted,

    /// <summary>The booking is awaiting host confirmation (requires-confirmation policy).</summary>
    Pending,

    /// <summary>Round-robin / collective bookings awaiting host acceptance.</summary>
    AwaitingHost,

    /// <summary>The booking has been cancelled by booker or host.</summary>
    Cancelled,

    /// <summary>The host explicitly rejected the booking request.</summary>
    Rejected,

    /// <summary>
    ///     The session has occurred and was marked completed by the host. Triggers the
    ///     after-session payment dispatch when <c>PaymentTiming = AfterSession</c>.
    /// </summary>
    Completed
}

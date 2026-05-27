using JetBrains.Annotations;

namespace Main.Features.Scheduling.Domain;

/// <summary>
///     Categorises an event in a booking's <see cref="BookingHistoryEntry" /> audit trail.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingHistoryEventType
{
    Created,
    Confirmed,
    Rejected,
    Rescheduled,
    Cancelled,
    NoShow,
    LocationChanged,
    GuestAdded,
    Reassigned,
    Rated,
    SeatReserved,
    SeatReleased,
    Completed
}

namespace Main.Features.Scheduling.Domain;

/// <summary>
///     Constants for the booking status strings persisted in the database.
/// </summary>
internal static class BookingStatuses
{
    internal const string Accepted = "accepted";
    internal const string Pending = "pending";
    internal const string Cancelled = "cancelled";
    internal const string Rejected = "rejected";
}

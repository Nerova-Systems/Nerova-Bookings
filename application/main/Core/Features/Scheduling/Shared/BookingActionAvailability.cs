using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;

namespace Main.Features.Scheduling.Shared;

public sealed record BookingActionsResponse(
    BookingActionResponse Cancel,
    BookingActionResponse Reschedule,
    BookingActionResponse RequestReschedule,
    BookingActionResponse EditLocation,
    BookingActionResponse AddGuests,
    BookingActionResponse ViewRecordings,
    BookingActionResponse ViewSessionDetails,
    BookingActionResponse MarkNoShow,
    BookingActionResponse Report
);

public sealed record BookingActionResponse(bool Visible, bool Enabled, string? DisabledReason);

public static class BookingActionAvailability
{
    public static BookingActionsResponse Resolve(Booking booking, EventType eventType, DateTimeOffset now)
    {
        return new BookingActionsResponse(
            ResolveCancel(booking, eventType, now),
            Disabled("Reschedule booking is not implemented yet."),
            Disabled("Request reschedule is not implemented yet."),
            Disabled("Edit location is not implemented yet."),
            Disabled("Add guests is not implemented yet."),
            Disabled("Recordings are not available until conferencing is ported."),
            Disabled("Session details are not available until conferencing is ported."),
            Disabled("No-show tracking is not implemented yet."),
            Disabled("Report booking is not implemented yet.")
        );
    }

    public static BookingActionResponse ResolveCancel(Booking booking, EventType eventType, DateTimeOffset now)
    {
        if (booking.Status == BookingStatus.Cancelled)
        {
            return Disabled("Cancelled bookings cannot be cancelled.");
        }

        if (booking.Status == BookingStatus.Rejected)
        {
            return Disabled("Rejected bookings cannot be cancelled.");
        }

        if (booking.EndTime <= now)
        {
            return Disabled("Past bookings cannot be cancelled.");
        }

        if (!eventType.Settings.CancellationPolicy.AllowCancellation)
        {
            return Disabled("Cancellation is disabled for this event type.");
        }

        var minimumNoticeMinutes = eventType.Settings.CancellationPolicy.MinimumNoticeMinutes;
        if (minimumNoticeMinutes is not null && booking.StartTime < now.AddMinutes(minimumNoticeMinutes.Value))
        {
            return Disabled("This booking is inside the minimum cancellation notice.");
        }

        return new BookingActionResponse(true, true, null);
    }

    private static BookingActionResponse Disabled(string reason)
    {
        return new BookingActionResponse(true, false, reason);
    }
}

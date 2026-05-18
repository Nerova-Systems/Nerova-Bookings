using Main.Features.EventTypes.Domain;
using Main.Features.Scheduling.Domain;

namespace Main.Features.Scheduling.Shared;

public sealed record BookingActionsResponse(
    BookingActionResponse Confirm,
    BookingActionResponse Reject,
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
            ResolvePendingBookingAction(booking),
            ResolvePendingBookingAction(booking),
            ResolveCancel(booking, eventType, now),
            ResolveReschedule(booking, eventType, now),
            ResolveReschedule(booking, eventType, now),
            ResolveMutableAcceptedBookingAction(booking, "Cancelled or rejected bookings cannot change location."),
            ResolveMutableAcceptedBookingAction(booking, "Cancelled or rejected bookings cannot add guests."),
            Disabled("Recordings are not available until conferencing is ported."),
            Disabled("Session details are not available until conferencing is ported."),
            Disabled("No-show tracking is not implemented yet."),
            Disabled("Report booking is not implemented yet.")
        );
    }

    public static BookingActionResponse ResolveCancel(Booking booking, EventType eventType, DateTimeOffset now)
    {
        var normalizedStatus = booking.Status.Trim().ToLowerInvariant();
        if (normalizedStatus == "cancelled")
        {
            return Disabled("Cancelled bookings cannot be cancelled.");
        }

        if (normalizedStatus == "rejected")
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

    public static BookingActionResponse ResolveReschedule(Booking booking, EventType eventType, DateTimeOffset now)
    {
        var normalizedStatus = booking.Status.Trim().ToLowerInvariant();
        if (normalizedStatus is "cancelled" or "rejected")
        {
            return Disabled("Cancelled or rejected bookings cannot be rescheduled.");
        }

        if (booking.EndTime <= now)
        {
            return Disabled("Past bookings cannot be rescheduled.");
        }

        if (!eventType.Settings.ReschedulePolicy.AllowReschedule)
        {
            return Disabled("Rescheduling is disabled for this event type.");
        }

        var minimumNoticeMinutes = eventType.Settings.ReschedulePolicy.MinimumNoticeMinutes;
        if (minimumNoticeMinutes is not null && booking.StartTime < now.AddMinutes(minimumNoticeMinutes.Value))
        {
            return Disabled("This booking is inside the minimum reschedule notice.");
        }

        return new BookingActionResponse(true, true, null);
    }

    private static BookingActionResponse ResolveMutableAcceptedBookingAction(Booking booking, string disabledReason)
    {
        return booking.Status.Trim().ToLowerInvariant() is "cancelled" or "rejected"
            ? Disabled(disabledReason)
            : new BookingActionResponse(true, true, null);
    }

    private static BookingActionResponse ResolvePendingBookingAction(Booking booking)
    {
        return booking.Status.Trim().ToLowerInvariant() == "pending"
            ? new BookingActionResponse(true, true, null)
            : new BookingActionResponse(false, false, null);
    }

    private static BookingActionResponse Disabled(string reason)
    {
        return new BookingActionResponse(true, false, reason);
    }
}

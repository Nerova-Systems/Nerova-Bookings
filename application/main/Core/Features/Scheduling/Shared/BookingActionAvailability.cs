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
    BookingActionResponse Report,
    BookingActionResponse Confirm,
    BookingActionResponse Reject,
    BookingActionResponse Rate,
    BookingActionResponse Reassign
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
            Disabled("Reschedule booking is not implemented yet."),
            ResolveRequestReschedule(booking, now),
            ResolveEditLocation(booking),
            ResolveAddGuests(booking, now),
            Disabled("Recordings are not available until conferencing is ported."),
            Disabled("Session details are not available until conferencing is ported."),
            ResolveMarkNoShow(booking, now),
            Disabled("Report booking is not implemented yet."),
            ResolveConfirm(booking),
            ResolveReject(booking),
            ResolveRate(booking, now),
            ResolveReassign(booking)
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

    public static BookingActionResponse ResolveConfirm(Booking booking)
    {
        if (booking.Status is BookingStatus.Pending or BookingStatus.AwaitingHost)
        {
            return new BookingActionResponse(true, true, null);
        }

        return Disabled("Only awaiting-confirmation bookings can be confirmed.");
    }

    public static BookingActionResponse ResolveReject(Booking booking)
    {
        if (booking.Status is BookingStatus.Pending or BookingStatus.AwaitingHost)
        {
            return new BookingActionResponse(true, true, null);
        }

        return Disabled("Only awaiting-confirmation bookings can be rejected.");
    }

    public static BookingActionResponse ResolveRequestReschedule(Booking booking, DateTimeOffset now)
    {
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Disabled("Closed bookings cannot be rescheduled.");
        }

        if (booking.EndTime <= now)
        {
            return Disabled("Past bookings cannot be rescheduled.");
        }

        return new BookingActionResponse(true, true, null);
    }

    public static BookingActionResponse ResolveEditLocation(Booking booking)
    {
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Disabled("Closed bookings cannot have their location edited.");
        }

        return new BookingActionResponse(true, true, null);
    }

    public static BookingActionResponse ResolveAddGuests(Booking booking, DateTimeOffset now)
    {
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Disabled("Closed bookings cannot have guests added.");
        }

        if (booking.EndTime <= now)
        {
            return Disabled("Past bookings cannot have guests added.");
        }

        return new BookingActionResponse(true, true, null);
    }

    public static BookingActionResponse ResolveMarkNoShow(Booking booking, DateTimeOffset now)
    {
        if (booking.EndTime > now)
        {
            return Disabled("No-show can only be recorded after the booking has ended.");
        }

        return new BookingActionResponse(true, true, null);
    }

    public static BookingActionResponse ResolveRate(Booking booking, DateTimeOffset now)
    {
        if (booking.Status != BookingStatus.Accepted)
        {
            return Disabled("Only accepted bookings can be rated.");
        }

        if (booking.EndTime > now)
        {
            return Disabled("Bookings can only be rated after they end.");
        }

        return new BookingActionResponse(true, true, null);
    }

    public static BookingActionResponse ResolveReassign(Booking booking)
    {
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return Disabled("Closed bookings cannot be reassigned.");
        }

        return new BookingActionResponse(true, true, null);
    }

    private static BookingActionResponse Disabled(string reason)
    {
        return new BookingActionResponse(true, false, reason);
    }
}

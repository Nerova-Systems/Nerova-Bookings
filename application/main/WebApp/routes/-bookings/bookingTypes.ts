import type { Schemas } from "@/shared/lib/api/client";

export const bookingStatuses = ["upcoming", "unconfirmed", "recurring", "past", "cancelled"] as const;

export type BookingStatusView = (typeof bookingStatuses)[number];

export type BookingListItem = Schemas["BookingListItemResponse"];

export function isBookingStatusView(status: string): status is BookingStatusView {
  return bookingStatuses.includes(status as BookingStatusView);
}

export function getBookingStatusLabel(status: BookingStatusView) {
  switch (status) {
    case "upcoming":
      return "Upcoming";
    case "unconfirmed":
      return "Unconfirmed";
    case "recurring":
      return "Recurring";
    case "past":
      return "Past";
    case "cancelled":
      return "Cancelled";
  }
}

export function getBookingEmptyDescription(status: BookingStatusView) {
  switch (status) {
    case "upcoming":
      return "Upcoming bookings will appear here after clients book time with you.";
    case "unconfirmed":
      return "Bookings waiting for approval will appear here.";
    case "recurring":
      return "Recurring event bookings will appear here when recurring event types are booked.";
    case "past":
      return "Completed bookings will move here after their end time.";
    case "cancelled":
      return "Cancelled and rejected bookings will appear here.";
  }
}

export function formatBookingDateRange(booking: BookingListItem) {
  const start = new Date(booking.startTime);
  const end = new Date(booking.endTime);
  const dateFormatter = new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    year: start.getFullYear() === new Date().getFullYear() ? undefined : "numeric"
  });
  const timeFormatter = new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
    timeZone: booking.timeZone
  });

  return `${dateFormatter.format(start)}, ${timeFormatter.format(start)} - ${timeFormatter.format(end)}`;
}

export function getStatusVariant(status: string): "default" | "secondary" | "destructive" | "outline" {
  switch (status.toLowerCase()) {
    case "accepted":
      return "default";
    case "pending":
      return "secondary";
    case "cancelled":
    case "rejected":
      return "destructive";
    default:
      return "outline";
  }
}

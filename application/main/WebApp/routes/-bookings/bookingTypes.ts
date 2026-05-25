import type { Schemas } from "@/shared/lib/api/client";

export const bookingStatuses = ["upcoming", "unconfirmed", "recurring", "past", "cancelled"] as const;
export const calendarBookingStatuses = ["upcoming", "unconfirmed", "recurring", "past"] as const;
export const bookingViews = ["list", "calendar"] as const;

export type BookingStatusView = (typeof bookingStatuses)[number];
export type BookingDashboardView = (typeof bookingViews)[number];

export type BookingListItem = Schemas["BookingListItemResponse"];

export function isBookingStatusView(status: string): status is BookingStatusView {
  return bookingStatuses.includes(status as BookingStatusView);
}

export function isBookingDashboardView(view: string | undefined): view is BookingDashboardView {
  return view === "list" || view === "calendar";
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

export function getActiveBookingFiltersCount(search: BookingFilterState) {
  const baseFilters = [
    search.search,
    search.eventTypeId,
    search.attendeeName,
    search.attendeeEmail,
    search.bookingUid,
    search.dateFrom,
    search.dateTo
  ].filter((value) => value !== undefined && value !== "").length;

  const toggleFilters =
    (search.noShowOnly ? 1 : 0) +
    (search.hasInternalNote ? 1 : 0) +
    (search.minRating !== undefined && search.minRating > 0 ? 1 : 0);

  return baseFilters + toggleFilters;
}

export function getWeekStartDate(date: Date, weekStartsOn = 1) {
  const nextDate = new Date(date);
  const diff = (nextDate.getDay() - weekStartsOn + 7) % 7;
  nextDate.setDate(nextDate.getDate() - diff);
  nextDate.setHours(0, 0, 0, 0);
  return nextDate;
}

export function toDateInputValue(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
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

export function formatBookingDuration(booking: BookingListItem) {
  const start = new Date(booking.startTime).getTime();
  const end = new Date(booking.endTime).getTime();
  const minutes = Math.max(0, Math.round((end - start) / 60_000));
  if (minutes < 60) {
    return `${minutes}m`;
  }
  const hours = Math.floor(minutes / 60);
  const remaining = minutes % 60;
  return remaining === 0 ? `${hours}h` : `${hours}h ${remaining}m`;
}

export interface BookingFilterState {
  search?: string;
  eventTypeId?: string;
  attendeeName?: string;
  attendeeEmail?: string;
  bookingUid?: string;
  dateFrom?: string;
  dateTo?: string;
  noShowOnly?: boolean;
  hasInternalNote?: boolean;
  minRating?: number;
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

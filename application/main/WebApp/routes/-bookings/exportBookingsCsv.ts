import type { BookingListItem } from "./bookingTypes";

const csvHeaders = [
  "Booking ID",
  "Status",
  "Event type",
  "Attendee name",
  "Attendee email",
  "Start time",
  "End time",
  "Time zone",
  "Location",
  "Recurring"
] as const;

function escapeCsvCell(value: string): string {
  if (value.includes(",") || value.includes("\"") || value.includes("\n") || value.includes("\r")) {
    return `"${value.replace(/"/g, "\"\"")}"`;
  }
  return value;
}

function bookingToRow(booking: BookingListItem): string[] {
  const location = booking.locationValue ?? booking.locationType ?? "";
  return [
    booking.id,
    booking.status,
    booking.eventTypeTitle,
    booking.bookerName,
    booking.bookerEmail,
    booking.startTime,
    booking.endTime,
    booking.timeZone,
    location,
    booking.isRecurring ? "Yes" : "No"
  ];
}

export function downloadBookingsCsv(bookings: readonly BookingListItem[], filename: string): void {
  const rows = [csvHeaders.map(escapeCsvCell).join(","), ...bookings.map((booking) => bookingToRow(booking).map(escapeCsvCell).join(","))];
  const csvContent = `\uFEFF${rows.join("\r\n")}`;
  const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);

  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

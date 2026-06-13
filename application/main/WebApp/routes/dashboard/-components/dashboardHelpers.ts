import { t } from "@lingui/core/macro";

import type { BookingListItem } from "../../-bookings/bookingTypes";

export function getTimeBasedGreeting(firstName: string | undefined) {
  const hour = new Date().getHours();
  if (hour >= 0 && hour < 5) {
    return firstName ? t`Burning the midnight oil, ${firstName}?` : t`Burning the midnight oil?`;
  }
  if (hour >= 5 && hour < 12) {
    return firstName ? t`Good morning, ${firstName}` : t`Good morning`;
  }
  if (hour >= 12 && hour < 17) {
    return firstName ? t`Good afternoon, ${firstName}` : t`Good afternoon`;
  }
  return firstName ? t`Good evening, ${firstName}` : t`Good evening`;
}
export function getTodayRange() {
  const start = new Date();
  start.setHours(0, 0, 0, 0);
  const end = new Date(start);
  end.setHours(23, 59, 59, 999);

  return { start: start.toISOString(), end: end.toISOString() };
}

export function formatBookingTime(booking: BookingListItem) {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
    timeZone: booking.timeZone
  }).format(new Date(booking.startTime));
}

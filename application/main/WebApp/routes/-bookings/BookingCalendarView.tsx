import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { cn } from "@repo/ui/utils";
import { CalendarXIcon } from "lucide-react";

import type { BookingListItem } from "./bookingTypes";

const hours = Array.from({ length: 24 }, (_, hour) => hour);
const hourHeightRem = 3;

export function BookingCalendarView({
  bookings,
  weekStart,
  isLoading,
  selectedBookingId,
  onSelectBooking
}: Readonly<{
  bookings: BookingListItem[];
  weekStart: Date;
  isLoading: boolean;
  selectedBookingId: string | null;
  onSelectBooking: (booking: BookingListItem) => void;
}>) {
  const days = getWeekDays(weekStart);

  if (isLoading) {
    return <Skeleton className="h-[36rem] rounded-2xl border" data-testid="bookings-calendar-view" />;
  }

  if (bookings.length === 0) {
    return (
      <Empty className="min-h-96 border" data-testid="bookings-calendar-view">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <CalendarXIcon />
          </EmptyMedia>
          <EmptyTitle>
            <Trans>No bookings this week</Trans>
          </EmptyTitle>
          <EmptyDescription>
            <Trans>Bookings in the selected week will appear in the calendar view.</Trans>
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }

  return (
    <div className="overflow-hidden rounded-2xl border bg-background" data-testid="bookings-calendar-view">
      <div className="grid grid-cols-[4rem_repeat(7,minmax(8rem,1fr))] border-b bg-muted/40">
        <div className="border-r" />
        {days.map((day) => (
          <div key={day.toISOString()} className="border-r p-3 text-center last:border-r-0">
            <div className="text-xs text-muted-foreground">
              {new Intl.DateTimeFormat(undefined, { weekday: "short" }).format(day)}
            </div>
            <div className="font-medium">{new Intl.DateTimeFormat(undefined, { day: "numeric" }).format(day)}</div>
          </div>
        ))}
      </div>
      <div className="max-h-[calc(100dvh-18rem)] overflow-auto">
        <div className="grid min-w-[60rem] grid-cols-[4rem_repeat(7,minmax(8rem,1fr))]">
          <div className="border-r">
            {hours.map((hour) => (
              <div
                key={hour}
                className="border-b pr-2 text-right text-xs text-muted-foreground"
                style={{ height: `${hourHeightRem}rem` }}
              >
                {formatHour(hour)}
              </div>
            ))}
          </div>
          {days.map((day) => (
            <div key={day.toISOString()} className="relative border-r last:border-r-0">
              {hours.map((hour) => (
                <div key={hour} className="border-b" style={{ height: `${hourHeightRem}rem` }} />
              ))}
              {bookings
                .filter((booking) => isSameDate(new Date(booking.startTime), day))
                .map((booking) => (
                  <button
                    key={booking.id}
                    type="button"
                    data-booking-calendar-event
                    data-state={selectedBookingId === booking.id ? "selected" : undefined}
                    className={cn(
                      "absolute right-1 left-1 cursor-pointer overflow-hidden rounded-lg border border-primary/30 bg-primary/15 p-2 text-left text-xs shadow-xs outline-primary transition-colors hover:bg-primary/25 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 active:bg-primary/30",
                      selectedBookingId === booking.id && "border-primary bg-primary/25 shadow-md",
                      booking.status.toLowerCase() === "pending" &&
                        "border-amber-500/40 bg-amber-500/15 hover:bg-amber-500/25",
                      ["cancelled", "rejected"].includes(booking.status.toLowerCase()) &&
                        "border-destructive/40 bg-destructive/15 hover:bg-destructive/25"
                    )}
                    style={getBookingPositionStyle(booking)}
                    onClick={() => onSelectBooking(booking)}
                  >
                    <span className="block truncate font-medium">{booking.eventTypeTitle}</span>
                    <span className="block truncate text-muted-foreground">{booking.bookerName}</span>
                    <span className="block truncate text-muted-foreground">{formatBookingTime(booking)}</span>
                  </button>
                ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function getWeekDays(weekStart: Date) {
  return Array.from({ length: 7 }, (_, index) => {
    const day = new Date(weekStart);
    day.setDate(day.getDate() + index);
    return day;
  });
}

function isSameDate(left: Date, right: Date) {
  return (
    left.getFullYear() === right.getFullYear() &&
    left.getMonth() === right.getMonth() &&
    left.getDate() === right.getDate()
  );
}

function getBookingPositionStyle(booking: BookingListItem) {
  const startTime = new Date(booking.startTime);
  const endTime = new Date(booking.endTime);
  const startMinutes = startTime.getHours() * 60 + startTime.getMinutes();
  const durationMinutes = Math.max(15, (endTime.getTime() - startTime.getTime()) / 60_000);
  return {
    top: `${(startMinutes / 60) * hourHeightRem}rem`,
    height: `${Math.max(1.75, (durationMinutes / 60) * hourHeightRem)}rem`
  };
}

function formatHour(hour: number) {
  return new Intl.DateTimeFormat(undefined, { hour: "numeric" }).format(new Date(2026, 0, 1, hour));
}

function formatBookingTime(booking: BookingListItem) {
  const formatter = new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
    timeZone: booking.timeZone
  });
  return `${formatter.format(new Date(booking.startTime))} - ${formatter.format(new Date(booking.endTime))}`;
}

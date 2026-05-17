import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { CalendarIcon, CalendarXIcon, MailIcon, UserIcon } from "lucide-react";

import { BookingActionsDropdown } from "./BookingActionsDropdown";
import {
  type BookingListItem,
  type BookingStatusView,
  formatBookingDateRange,
  getBookingEmptyDescription,
  getBookingStatusLabel,
  getStatusVariant
} from "./bookingTypes";

export function BookingsList({
  bookings,
  status,
  isLoading,
  onSelectBooking
}: Readonly<{
  bookings: BookingListItem[];
  status: BookingStatusView;
  isLoading: boolean;
  onSelectBooking: (booking: BookingListItem) => void;
}>) {
  if (isLoading) {
    return (
      <div className="overflow-hidden rounded-md border bg-background">
        {Array.from({ length: 5 }).map((_, index) => (
          <div key={index} className="grid gap-4 border-b p-4 last:border-b-0 sm:grid-cols-[12rem_1fr_auto]">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-14 w-full" />
            <Skeleton className="h-9 w-28" />
          </div>
        ))}
      </div>
    );
  }

  if (bookings.length === 0) {
    return (
      <Empty className="min-h-64 border">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <CalendarXIcon />
          </EmptyMedia>
          <EmptyTitle>
            <Trans>No {getBookingStatusLabel(status).toLowerCase()} bookings yet</Trans>
          </EmptyTitle>
          <EmptyDescription>
            <Trans>{getBookingEmptyDescription(status)}</Trans>
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }

  return (
    <div className="overflow-hidden rounded-md border bg-background" data-testid={`${status}-bookings`}>
      {bookings.map((booking) => (
        <article
          key={booking.id}
          data-testid="booking-item"
          className="group grid cursor-pointer gap-3 border-b p-4 transition-colors last:border-b-0 hover:bg-muted/40 active:bg-muted/60 sm:grid-cols-[13rem_1fr_auto] sm:items-center"
          onClick={() => onSelectBooking(booking)}
          onKeyDown={(event) => {
            if (event.key === "Enter") onSelectBooking(booking);
          }}
          tabIndex={0}
        >
          <div className="flex min-w-0 flex-col gap-1">
            <div className="flex items-center gap-2 text-sm font-medium">
              <CalendarIcon className="size-4 text-muted-foreground" />
              <span>
                {new Intl.DateTimeFormat(undefined, { weekday: "short", month: "short", day: "numeric" }).format(
                  new Date(booking.startTime)
                )}
              </span>
            </div>
            <span className="text-sm text-muted-foreground">
              {new Intl.DateTimeFormat(undefined, {
                hour: "numeric",
                minute: "2-digit",
                timeZone: booking.timeZone
              }).format(new Date(booking.startTime))}
              {" - "}
              {new Intl.DateTimeFormat(undefined, {
                hour: "numeric",
                minute: "2-digit",
                timeZone: booking.timeZone
              }).format(new Date(booking.endTime))}
            </span>
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="truncate text-base font-medium">{booking.eventTypeTitle}</h2>
              <Badge variant={getStatusVariant(booking.status)}>{booking.status}</Badge>
              {booking.isRecurring && (
                <Badge variant="outline">
                  <Trans>Recurring</Trans>
                </Badge>
              )}
            </div>
            <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
              <span className="inline-flex items-center gap-1">
                <UserIcon className="size-4" />
                {booking.bookerName}
              </span>
              <span className="inline-flex items-center gap-1">
                <MailIcon className="size-4" />
                {booking.bookerEmail}
              </span>
            </div>
            <span className="mt-1 block truncate text-xs text-muted-foreground">{formatBookingDateRange(booking)}</span>
          </div>
          <div className="flex justify-end">
            <BookingActionsDropdown booking={booking} />
          </div>
        </article>
      ))}
    </div>
  );
}

import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { cn } from "@repo/ui/utils";
import { CalendarIcon, LinkIcon, MailIcon, UserIcon } from "lucide-react";

import { BookingActionsDropdown } from "./BookingActionsDropdown";
import { type BookingListItem as BookingListItemType, formatBookingDateRange, getStatusVariant } from "./bookingTypes";

export function BookingListItem({
  booking,
  isSelected,
  onSelectBooking
}: Readonly<{
  booking: BookingListItemType;
  isSelected: boolean;
  onSelectBooking: (booking: BookingListItemType) => void;
}>) {
  const startTime = new Date(booking.startTime);
  const endTime = new Date(booking.endTime);
  const isCancelled = ["cancelled", "rejected"].includes(booking.status.toLowerCase());

  return (
    <article
      data-testid="booking-item"
      data-booking-list-item
      data-state={isSelected ? "selected" : undefined}
      className={cn(
        "group relative w-full border-b transition-colors last:border-b-0 hover:bg-muted/40",
        isSelected &&
          "bg-muted/60 shadow-[inset_0.1875rem_0_0_var(--primary)] before:absolute before:top-0 before:left-0 before:h-full before:w-0.5 before:bg-primary"
      )}
    >
      <div className="flex flex-col sm:flex-row">
        <Button
          type="button"
          variant="ghost"
          className="hidden h-auto min-w-44 justify-start rounded-none py-4 pr-3 pl-5 text-left active:bg-muted/60 sm:flex"
          onClick={() => onSelectBooking(booking)}
        >
          <div className="flex min-w-0 flex-col gap-1">
            <span className="text-sm font-medium">{formatBookingDay(startTime)}</span>
            <span className="text-sm text-muted-foreground">
              {formatBookingTime(startTime, booking.timeZone)} - {formatBookingTime(endTime, booking.timeZone)}
            </span>
            {booking.locationValue && (
              <span className="inline-flex min-w-0 items-center gap-1 truncate text-sm text-primary">
                <LinkIcon className="size-4" />
                <Trans>Join meeting</Trans>
              </span>
            )}
          </div>
        </Button>
        <Button
          type="button"
          variant="ghost"
          className="flex h-auto min-w-0 flex-1 flex-col items-stretch justify-start gap-3 rounded-none px-5 py-4 text-left active:bg-muted/60"
          onClick={() => onSelectBooking(booking)}
        >
          <div className="flex w-full items-start justify-between gap-3 sm:hidden">
            <div className="min-w-0">
              <div className="text-sm font-medium">{formatBookingDay(startTime)}</div>
              <div className="text-sm text-muted-foreground">
                {formatBookingTime(startTime, booking.timeZone)} - {formatBookingTime(endTime, booking.timeZone)}
              </div>
            </div>
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2
                className={cn(
                  "truncate text-sm leading-5 font-medium",
                  isCancelled && "text-muted-foreground line-through"
                )}
              >
                {booking.eventTypeTitle}
              </h2>
              <Badge variant={getStatusVariant(booking.status)}>{booking.status}</Badge>
              {booking.isRecurring && (
                <Badge variant="outline">
                  <Trans>Recurring</Trans>
                </Badge>
              )}
            </div>
            {booking.description && (
              <span className="mt-1 block truncate text-sm text-muted-foreground">
                &quot;{booking.description}&quot;
              </span>
            )}
            <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
              <span className="inline-flex items-center gap-1">
                <UserIcon className="size-4" />
                {booking.bookerName}
              </span>
              <span className="inline-flex items-center gap-1">
                <MailIcon className="size-4" />
                {booking.bookerEmail}
              </span>
              <span className="inline-flex items-center gap-1 sm:hidden">
                <CalendarIcon className="size-4" />
                {formatBookingDateRange(booking)}
              </span>
            </div>
            {booking.locationValue && (
              <span className="mt-2 inline-flex min-w-0 items-center gap-1 truncate text-sm text-primary sm:hidden">
                <LinkIcon className="size-4" />
                <Trans>Join meeting</Trans>
              </span>
            )}
          </div>
        </Button>
        <div className="flex items-start justify-end px-5 pb-4 sm:py-4 sm:pr-5 sm:pl-0">
          <BookingActionsDropdown booking={booking} />
        </div>
      </div>
    </article>
  );
}

function formatBookingDay(date: Date) {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    day: "numeric",
    month: "short",
    year: date.getFullYear() === new Date().getFullYear() ? undefined : "numeric"
  }).format(date);
}

function formatBookingTime(date: Date, timeZone: string) {
  return new Intl.DateTimeFormat(undefined, {
    hour: "numeric",
    minute: "2-digit",
    timeZone
  }).format(date);
}

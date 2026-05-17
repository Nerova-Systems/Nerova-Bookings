import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { CalendarXIcon } from "lucide-react";

import { BookingListItem as BookingListRow } from "./BookingListItem";
import {
  type BookingListItem,
  type BookingStatusView,
  getBookingEmptyDescription,
  getBookingStatusLabel
} from "./bookingTypes";

export function BookingsList({
  bookings,
  status,
  isLoading,
  selectedBookingId,
  onSelectBooking
}: Readonly<{
  bookings: BookingListItem[];
  status: BookingStatusView;
  isLoading: boolean;
  selectedBookingId: string | null;
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
        <BookingListRow
          key={booking.id}
          booking={booking}
          isSelected={selectedBookingId === booking.id}
          onSelectBooking={onSelectBooking}
        />
      ))}
    </div>
  );
}

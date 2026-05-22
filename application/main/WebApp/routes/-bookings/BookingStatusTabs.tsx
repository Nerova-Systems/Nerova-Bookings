import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { useNavigate } from "@tanstack/react-router";

import { type BookingStatusView, bookingStatuses, getBookingStatusLabel } from "./bookingTypes";

export function BookingStatusTabs({
  status,
  search
}: Readonly<{ status: BookingStatusView; search: BookingsRouteSearch }>) {
  const navigate = useNavigate();

  return (
    <div className="overflow-x-auto">
      <div className="flex w-max rounded-md border bg-background p-1">
        {bookingStatuses.map((bookingStatus) => (
          <Button
            key={bookingStatus}
            type="button"
            size="sm"
            variant={bookingStatus === status ? "default" : "ghost"}
            aria-label={getBookingStatusLabel(bookingStatus)}
            onClick={() => navigate({ to: "/bookings/$status", params: { status: bookingStatus }, search })}
          >
            <Trans>{getBookingStatusLabel(bookingStatus)}</Trans>
          </Button>
        ))}
      </div>
    </div>
  );
}

export interface BookingsRouteSearch {
  search: string | undefined;
  eventTypeId: string | undefined;
  attendeeName: string | undefined;
  attendeeEmail: string | undefined;
  bookingUid: string | undefined;
  dateFrom: string | undefined;
  dateTo: string | undefined;
  noShowOnly: boolean | undefined;
  hasInternalNote: boolean | undefined;
  minRating: number | undefined;
  view: "list" | "calendar";
  weekStart: string;
  pageOffset: number;
}

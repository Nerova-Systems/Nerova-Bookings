import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { type RowKey, Table, TableBody } from "@repo/ui/components/Table";
import { CalendarXIcon } from "lucide-react";

import { BookingsTableHeader } from "./BookingsTableHeader";
import { BookingsTableRow } from "./BookingsTableRow";
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
  selectedKeys,
  onSelectionChange,
  onActivate
}: Readonly<{
  bookings: BookingListItem[];
  status: BookingStatusView;
  isLoading: boolean;
  selectedKeys: ReadonlySet<RowKey>;
  onSelectionChange: (keys: ReadonlySet<RowKey>) => void;
  onActivate: (booking: BookingListItem) => void;
}>) {
  if (isLoading) {
    return (
      <div className="mb-6 overflow-hidden rounded-2xl border bg-background" data-testid={`${status}-bookings`}>
        {Array.from({ length: 5 }).map((_, index) => (
          <div
            // biome-ignore lint/suspicious/noArrayIndexKey: skeleton placeholders
            key={index}
            className="grid gap-4 border-b p-4 last:border-b-0 sm:grid-cols-[12rem_1fr_auto]"
          >
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
      <Empty className="mb-6 min-h-80 rounded-2xl border" data-testid={`${status}-bookings`}>
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
    <Table
      data-testid={`${status}-bookings`}
      rowSize="spacious"
      selectionMode="multiple"
      selectedKeys={selectedKeys}
      onSelectionChange={onSelectionChange}
      onActivate={(key) => {
        const booking = bookings.find((current) => current.id === key);
        if (booking) {
          onActivate(booking);
        }
      }}
    >
      <BookingsTableHeader />
      <TableBody>
        {bookings.map((booking) => (
          <BookingsTableRow key={booking.id} booking={booking} isSelected={selectedKeys.has(booking.id)} />
        ))}
      </TableBody>
    </Table>
  );
}

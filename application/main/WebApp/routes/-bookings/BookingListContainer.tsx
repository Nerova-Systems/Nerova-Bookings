import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { BookmarkIcon } from "lucide-react";
import { useState } from "react";

import type { EventType } from "../-scheduling/schedulingTypes";

import { ActiveBookingFilters } from "./ActiveBookingFilters";
import { BookingDetailsSheet } from "./BookingDetailsSheet";
import { BookingsFilters, type BookingFilterSearch } from "./BookingsFilters";
import { BookingsList } from "./BookingsList";
import { BookingStatusTabs, type BookingsRouteSearch } from "./BookingStatusTabs";
import {
  type BookingDashboardView,
  type BookingListItem,
  type BookingStatusView,
  getBookingStatusLabel
} from "./bookingTypes";
import { BookingViewToggleButton } from "./BookingViewToggleButton";

export function BookingListContainer({
  status,
  search,
  eventTypes,
  bookings,
  totalCount,
  pageSize,
  isLoading,
  view,
  onViewChange,
  onSearchChange,
  onPageOffsetChange
}: Readonly<{
  status: BookingStatusView;
  search: BookingsRouteSearch;
  eventTypes: EventType[];
  bookings: BookingListItem[];
  totalCount: number;
  pageSize: number;
  isLoading: boolean;
  view: BookingDashboardView;
  onViewChange: (view: BookingDashboardView) => void;
  onSearchChange: (search: BookingFilterSearch) => void;
  onPageOffsetChange: (pageOffset: number) => void;
}>) {
  const [selectedBooking, setSelectedBooking] = useState<BookingListItem | null>(null);
  const pageOffset = search.pageOffset;
  const canGoPrevious = pageOffset > 0;
  const canGoNext = pageOffset + pageSize < totalCount;

  return (
    <>
      <div className="flex flex-wrap items-center gap-2">
        <div className="w-full md:w-auto">
          <BookingStatusTabs status={status} search={search} />
        </div>
        <BookingsFilters eventTypes={eventTypes} search={search} onSearchChange={onSearchChange} />
        <div className="hidden grow md:block" />
        <Button type="button" variant="secondary" size="sm" disabled title={t`Saved filters are not implemented yet.`}>
          <BookmarkIcon />
          <Trans>Saved filters</Trans>
        </Button>
        <BookingViewToggleButton view={view} onViewChange={onViewChange} />
      </div>
      <ActiveBookingFilters eventTypes={eventTypes} search={search} onSearchChange={onSearchChange} />
      <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
        <span className="text-sm text-muted-foreground">
          <Trans>
            {totalCount} {getBookingStatusLabel(status).toLowerCase()} bookings
          </Trans>
        </span>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            disabled={!canGoPrevious}
            onClick={() => onPageOffsetChange(Math.max(0, pageOffset - pageSize))}
          >
            <Trans>Previous</Trans>
          </Button>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            disabled={!canGoNext}
            onClick={() => onPageOffsetChange(pageOffset + pageSize)}
          >
            <Trans>Next</Trans>
          </Button>
        </div>
      </div>
      <div className="mt-4">
        <BookingsList
          bookings={bookings}
          status={status}
          isLoading={isLoading}
          selectedBookingId={selectedBooking?.id ?? null}
          onSelectBooking={setSelectedBooking}
        />
      </div>
      <BookingDetailsSheet
        booking={selectedBooking}
        isOpen={selectedBooking !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) setSelectedBooking(null);
        }}
      />
    </>
  );
}

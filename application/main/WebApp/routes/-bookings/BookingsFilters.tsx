import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@repo/ui/components/Dialog";
import { ListFilterIcon } from "lucide-react";
import { useState } from "react";

import type { EventType } from "../-scheduling/schedulingTypes";

import { BookingsFiltersBody } from "./BookingsFiltersBody";
import { getActiveBookingFiltersCount, type BookingFilterState } from "./bookingTypes";

export type BookingFilterSearch = BookingFilterState;

export function BookingsFilters({
  eventTypes,
  search,
  onSearchChange
}: Readonly<{
  eventTypes: EventType[];
  search: BookingFilterSearch;
  onSearchChange: (search: BookingFilterSearch) => void;
}>) {
  const [isOpen, setIsOpen] = useState(false);
  const activeFilterCount = getActiveBookingFiltersCount(search);

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen} trackingTitle="Bookings filters">
      <Button type="button" variant="secondary" size="sm" onClick={() => setIsOpen(true)}>
        <ListFilterIcon />
        <Trans>Filter</Trans>
        {activeFilterCount > 0 ? <Badge variant="secondary">{activeFilterCount}</Badge> : null}
        {activeFilterCount === 0 ? (
          <span className="text-muted-foreground">
            <Trans>No filters</Trans>
          </span>
        ) : null}
      </Button>
      <DialogContent className="sm:w-dialog-lg">
        <DialogHeader>
          <DialogTitle>
            <Trans>Filter bookings</Trans>
          </DialogTitle>
        </DialogHeader>
        <BookingsFiltersBody
          eventTypes={eventTypes}
          search={search}
          onSearchChange={onSearchChange}
          onClose={() => setIsOpen(false)}
        />
      </DialogContent>
    </Dialog>
  );
}

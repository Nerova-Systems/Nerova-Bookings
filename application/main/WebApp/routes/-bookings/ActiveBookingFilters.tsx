import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { BookmarkIcon, XIcon } from "lucide-react";

import type { EventType } from "../-scheduling/schedulingTypes";
import type { BookingFilterSearch } from "./BookingsFilters";

const emptyBookingFilterSearch: BookingFilterSearch = {
  search: undefined,
  eventTypeId: undefined,
  attendeeName: undefined,
  attendeeEmail: undefined,
  bookingUid: undefined,
  dateFrom: undefined,
  dateTo: undefined,
  noShowOnly: undefined,
  hasInternalNote: undefined,
  minRating: undefined
};

export function ActiveBookingFilters({
  eventTypes,
  search,
  onSearchChange
}: Readonly<{
  eventTypes: EventType[];
  search: BookingFilterSearch;
  onSearchChange: (search: BookingFilterSearch) => void;
}>) {
  const filters = getActiveBookingFilters(eventTypes, search);
  if (filters.length === 0) {
    return null;
  }

  return (
    <div data-testid="active-booking-filters" className="mt-3 flex flex-wrap items-center gap-2">
      {filters.map((filter) => (
        <Badge key={filter.key} variant="secondary">
          {filter.label}: {filter.value}
        </Badge>
      ))}
      <div className="hidden grow md:block" />
      <Button type="button" variant="ghost" size="sm" onClick={() => onSearchChange(emptyBookingFilterSearch)}>
        <XIcon />
        <Trans>Clear</Trans>
      </Button>
      <Button type="button" variant="secondary" size="sm" disabled title={t`Saved filters are not implemented yet.`}>
        <BookmarkIcon />
        <Trans>Save</Trans>
      </Button>
    </div>
  );
}

function getActiveBookingFilters(eventTypes: EventType[], search: BookingFilterSearch) {
  const eventType = eventTypes.find((item) => item.id === search.eventTypeId);
  return [
    search.search ? { key: "search", label: t`Search`, value: search.search } : null,
    search.eventTypeId ? { key: "eventType", label: t`Service`, value: eventType?.title ?? search.eventTypeId } : null,
    search.attendeeName ? { key: "attendeeName", label: t`Client name`, value: search.attendeeName } : null,
    search.attendeeEmail ? { key: "attendeeEmail", label: t`Client email`, value: search.attendeeEmail } : null,
    search.bookingUid ? { key: "bookingUid", label: t`Reference number`, value: search.bookingUid } : null,
    search.dateFrom ? { key: "dateFrom", label: t`From`, value: search.dateFrom } : null,
    search.dateTo ? { key: "dateTo", label: t`To`, value: search.dateTo } : null,
    search.noShowOnly ? { key: "noShowOnly", label: t`No-show only`, value: t`Yes` } : null,
    search.hasInternalNote ? { key: "hasInternalNote", label: t`Has internal note`, value: t`Yes` } : null,
    search.minRating ? { key: "minRating", label: t`Minimum rating`, value: `${search.minRating}+` } : null
  ].filter((filter): filter is { key: string; label: string; value: string } => filter !== null);
}

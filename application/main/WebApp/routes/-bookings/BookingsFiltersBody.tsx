import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DateField } from "@repo/ui/components/DateField";
import { DialogBody, DialogClose, DialogFooter } from "@repo/ui/components/Dialog";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SearchIcon, XIcon } from "lucide-react";
import { useState } from "react";

import type { EventType } from "../-scheduling/schedulingTypes";
import type { BookingFilterSearch } from "./BookingsFilters";

import { getActiveBookingFiltersCount } from "./bookingTypes";
import { FacetedFilters } from "./FacetedFilters";

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

export function BookingsFiltersBody({
  eventTypes,
  search,
  onSearchChange,
  onClose
}: Readonly<{
  eventTypes: EventType[];
  search: BookingFilterSearch;
  onSearchChange: (search: BookingFilterSearch) => void;
  onClose: () => void;
}>) {
  const [draftSearch, setDraftSearch] = useState(search);
  const updateSearch = (nextSearch: Partial<BookingFilterSearch>) => setDraftSearch({ ...draftSearch, ...nextSearch });
  const activeFilterCount = getActiveBookingFiltersCount(draftSearch);

  return (
    <>
      <DialogBody>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-2">
            <Label htmlFor="booking-search">
              <Trans>Search</Trans>
            </Label>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="booking-search"
                aria-label={t`Search bookings`}
                className="pl-9"
                value={draftSearch.search ?? ""}
                placeholder={t`Event, client, email, or reference number`}
                onChange={(event) => updateSearch({ search: event.currentTarget.value || undefined })}
              />
            </div>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="booking-event-type">
              <Trans>Service</Trans>
            </Label>
            <Select
              value={draftSearch.eventTypeId ?? "all"}
              onValueChange={(value) => {
                const selectedValue = value ?? "all";
                updateSearch({ eventTypeId: selectedValue === "all" ? undefined : selectedValue });
              }}
            >
              <SelectTrigger id="booking-event-type" className="w-full" aria-label={t`Service`}>
                <SelectValue>
                  {(value: string) =>
                    value === "all"
                      ? t`All services`
                      : (eventTypes.find((eventType) => eventType.id === value)?.title ?? t`Service`)
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">
                  <Trans>All services</Trans>
                </SelectItem>
                {eventTypes.map((eventType) => (
                  <SelectItem key={eventType.id} value={eventType.id}>
                    {eventType.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <TextFilter
            id="booking-attendee-name"
            label={t`Client name`}
            value={draftSearch.attendeeName}
            onChange={(value) => updateSearch({ attendeeName: value })}
          />
          <TextFilter
            id="booking-attendee-email"
            label={t`Client email`}
            value={draftSearch.attendeeEmail}
            onChange={(value) => updateSearch({ attendeeEmail: value })}
          />
          <TextFilter
            id="booking-uid"
            label={t`Reference number`}
            value={draftSearch.bookingUid}
            onChange={(value) => updateSearch({ bookingUid: value })}
          />
          <DateField
            name="booking-date-from"
            label={t`From`}
            value={draftSearch.dateFrom ?? ""}
            onChange={(value) => updateSearch({ dateFrom: value || undefined })}
          />
          <DateField
            name="booking-date-to"
            label={t`To`}
            value={draftSearch.dateTo ?? ""}
            onChange={(value) => updateSearch({ dateTo: value || undefined })}
          />
        </div>
        <div className="mt-4 flex flex-col gap-3 border-t pt-4">
          <FacetedFilters draftSearch={draftSearch} updateSearch={updateSearch} />
        </div>
      </DialogBody>
      <DialogFooter>
        <Button
          type="button"
          variant="secondary"
          disabled={activeFilterCount === 0}
          onClick={() => setDraftSearch(emptyBookingFilterSearch)}
        >
          <XIcon />
          <Trans>Clear</Trans>
        </Button>
        <DialogClose render={<Button type="reset" variant="secondary" />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button
          type="button"
          onClick={() => {
            onSearchChange(draftSearch);
            onClose();
          }}
        >
          <Trans>Apply filters</Trans>
        </Button>
      </DialogFooter>
    </>
  );
}

function TextFilter({
  id,
  label,
  value,
  onChange
}: Readonly<{ id: string; label: string; value: string | undefined; onChange: (value: string | undefined) => void }>) {
  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        aria-label={label}
        value={value ?? ""}
        onChange={(event) => onChange(event.currentTarget.value || undefined)}
      />
    </div>
  );
}

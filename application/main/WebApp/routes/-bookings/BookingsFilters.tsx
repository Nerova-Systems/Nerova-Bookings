import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { DateField } from "@repo/ui/components/DateField";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { ListFilterIcon, SearchIcon, XIcon } from "lucide-react";
import { useState } from "react";

import type { EventType } from "../-scheduling/schedulingTypes";

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

function BookingsFiltersBody({
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
                placeholder={t`Event, attendee, email, or booking ID`}
                onChange={(event) => updateSearch({ search: event.currentTarget.value || undefined })}
              />
            </div>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="booking-event-type">
              <Trans>Event type</Trans>
            </Label>
            <Select
              value={draftSearch.eventTypeId ?? "all"}
              onValueChange={(value) => {
                const selectedValue = value ?? "all";
                updateSearch({ eventTypeId: selectedValue === "all" ? undefined : selectedValue });
              }}
            >
              <SelectTrigger id="booking-event-type" className="w-full" aria-label={t`Event type`}>
                <SelectValue>
                  {(value: string) =>
                    value === "all"
                      ? t`All event types`
                      : (eventTypes.find((eventType) => eventType.id === value)?.title ?? t`Event type`)
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">
                  <Trans>All event types</Trans>
                </SelectItem>
                {eventTypes.map((eventType) => (
                  <SelectItem key={eventType.id} value={eventType.id}>
                    {eventType.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="booking-attendee-name">
              <Trans>Attendee name</Trans>
            </Label>
            <Input
              id="booking-attendee-name"
              aria-label={t`Attendee name`}
              value={draftSearch.attendeeName ?? ""}
              onChange={(event) => updateSearch({ attendeeName: event.currentTarget.value || undefined })}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="booking-attendee-email">
              <Trans>Attendee email</Trans>
            </Label>
            <Input
              id="booking-attendee-email"
              aria-label={t`Attendee email`}
              value={draftSearch.attendeeEmail ?? ""}
              onChange={(event) => updateSearch({ attendeeEmail: event.currentTarget.value || undefined })}
            />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="booking-uid">
              <Trans>Booking ID</Trans>
            </Label>
            <Input
              id="booking-uid"
              aria-label={t`Booking ID`}
              value={draftSearch.bookingUid ?? ""}
              onChange={(event) => updateSearch({ bookingUid: event.currentTarget.value || undefined })}
            />
          </div>
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
      </DialogBody>
      <DialogFooter>
        <Button type="button" variant="secondary" disabled={activeFilterCount === 0} onClick={() => setDraftSearch({})}>
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

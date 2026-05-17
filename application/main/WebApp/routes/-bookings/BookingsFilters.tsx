import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { DateField } from "@repo/ui/components/DateField";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SearchIcon, XIcon } from "lucide-react";

import type { EventType } from "../-scheduling/schedulingTypes";

export interface BookingFilterSearch {
  search?: string;
  eventTypeId?: string;
  attendeeName?: string;
  attendeeEmail?: string;
  bookingUid?: string;
  dateFrom?: string;
  dateTo?: string;
}

export function BookingsFilters({
  eventTypes,
  search,
  onSearchChange
}: Readonly<{
  eventTypes: EventType[];
  search: BookingFilterSearch;
  onSearchChange: (search: BookingFilterSearch) => void;
}>) {
  const updateSearch = (nextSearch: Partial<BookingFilterSearch>) => onSearchChange({ ...search, ...nextSearch });
  const hasFilters = Object.values(search).some((value) => value !== undefined && value !== "");

  return (
    <div className="rounded-md border bg-background p-3">
      <div className="grid gap-3 lg:grid-cols-[minmax(14rem,1.2fr)_minmax(11rem,0.8fr)_repeat(5,minmax(9rem,0.7fr))_auto] lg:items-end">
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
              value={search.search ?? ""}
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
            value={search.eventTypeId ?? "all"}
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
            value={search.attendeeName ?? ""}
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
            value={search.attendeeEmail ?? ""}
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
            value={search.bookingUid ?? ""}
            onChange={(event) => updateSearch({ bookingUid: event.currentTarget.value || undefined })}
          />
        </div>
        <DateField
          name="booking-date-from"
          label={t`From`}
          value={search.dateFrom ?? ""}
          onChange={(value) => updateSearch({ dateFrom: value || undefined })}
        />
        <DateField
          name="booking-date-to"
          label={t`To`}
          value={search.dateTo ?? ""}
          onChange={(value) => updateSearch({ dateTo: value || undefined })}
        />
        <Button type="button" variant="secondary" disabled={!hasFilters} onClick={() => onSearchChange({})}>
          <XIcon />
          <Trans>Clear</Trans>
        </Button>
      </div>
    </div>
  );
}

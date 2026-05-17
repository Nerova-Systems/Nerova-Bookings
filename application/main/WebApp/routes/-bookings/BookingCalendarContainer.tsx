import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { ButtonGroup } from "@repo/ui/components/ButtonGroup";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { BookmarkIcon, ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useState } from "react";

import { api } from "@/shared/lib/api/client";

import type { EventType } from "../-scheduling/schedulingTypes";

import { ActiveBookingFilters } from "./ActiveBookingFilters";
import { BookingCalendarView } from "./BookingCalendarView";
import { BookingDetailsSheet } from "./BookingDetailsSheet";
import { BookingsFilters, type BookingFilterSearch } from "./BookingsFilters";
import { BookingStatusTabs, type BookingsRouteSearch } from "./BookingStatusTabs";
import {
  type BookingDashboardView,
  type BookingListItem,
  type BookingStatusView,
  calendarBookingStatuses,
  getWeekStartDate
} from "./bookingTypes";
import { BookingViewToggleButton } from "./BookingViewToggleButton";
import { formatWeekStartSearchValue, parseWeekStart, WeekPicker } from "./WeekPicker";

export function BookingCalendarContainer({
  status,
  search,
  eventTypes,
  view,
  onViewChange,
  onSearchChange,
  onWeekStartChange
}: Readonly<{
  status: BookingStatusView;
  search: BookingsRouteSearch;
  eventTypes: EventType[];
  view: BookingDashboardView;
  onViewChange: (view: BookingDashboardView) => void;
  onSearchChange: (search: BookingFilterSearch) => void;
  onWeekStartChange: (weekStart: Date) => void;
}>) {
  const [selectedBooking, setSelectedBooking] = useState<BookingListItem | null>(null);
  const weekStart = parseWeekStart(search.weekStart);
  const weekEnd = new Date(weekStart);
  weekEnd.setDate(weekEnd.getDate() + 6);
  weekEnd.setHours(23, 59, 59, 999);
  const { data, isLoading } = api.useQuery("get", "/api/bookings", {
    params: {
      query: {
        Statuses: [...calendarBookingStatuses],
        Search: search.search,
        EventTypeId: search.eventTypeId,
        AttendeeName: search.attendeeName,
        AttendeeEmail: search.attendeeEmail,
        BookingUid: search.bookingUid,
        AfterStartDate: weekStart.toISOString(),
        BeforeEndDate: weekEnd.toISOString(),
        PageOffset: 0,
        PageSize: 100
      }
    }
  });

  const updateWeekStart = (nextWeekStart: Date) => onWeekStartChange(getWeekStartDate(nextWeekStart));

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <div className="w-full md:w-auto">
            <BookingStatusTabs status={status} search={search} />
          </div>
          <WeekPicker weekStart={weekStart} onWeekStartChange={updateWeekStart} />
          <BookingsFilters eventTypes={eventTypes} search={search} onSearchChange={onSearchChange} />
        </div>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            disabled
            title={t`Saved filters are not implemented yet.`}
          >
            <BookmarkIcon />
            <Trans>Saved filters</Trans>
          </Button>
          <Button type="button" variant="secondary" size="sm" onClick={() => updateWeekStart(new Date())}>
            <Trans>Today</Trans>
          </Button>
          <ButtonGroup>
            <Tooltip>
              <TooltipTrigger
                render={
                  <Button
                    type="button"
                    variant="secondary"
                    size="icon-sm"
                    aria-label={t`View previous week`}
                    onClick={() => updateWeekStart(addWeeks(weekStart, -1))}
                  >
                    <ChevronLeftIcon />
                  </Button>
                }
              />
              <TooltipContent>{t`View previous week`}</TooltipContent>
            </Tooltip>
            <Tooltip>
              <TooltipTrigger
                render={
                  <Button
                    type="button"
                    variant="secondary"
                    size="icon-sm"
                    aria-label={t`View next week`}
                    onClick={() => updateWeekStart(addWeeks(weekStart, 1))}
                  >
                    <ChevronRightIcon />
                  </Button>
                }
              />
              <TooltipContent>{t`View next week`}</TooltipContent>
            </Tooltip>
          </ButtonGroup>
          <BookingViewToggleButton view={view} onViewChange={onViewChange} />
        </div>
      </div>
      <ActiveBookingFilters eventTypes={eventTypes} search={search} onSearchChange={onSearchChange} />
      <BookingCalendarView
        bookings={data?.bookings ?? []}
        weekStart={weekStart}
        isLoading={isLoading}
        selectedBookingId={selectedBooking?.id ?? null}
        onSelectBooking={setSelectedBooking}
      />
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

function addWeeks(date: Date, weeks: number) {
  const nextDate = new Date(formatWeekStartSearchValue(date));
  nextDate.setDate(nextDate.getDate() + weeks * 7);
  return nextDate;
}

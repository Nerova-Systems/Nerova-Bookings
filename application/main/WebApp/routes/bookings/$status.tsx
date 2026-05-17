import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";

import { api } from "@/shared/lib/api/client";

import { BookingDetailsSheet } from "../-bookings/BookingDetailsSheet";
import { BookingsFilters, type BookingFilterSearch } from "../-bookings/BookingsFilters";
import { BookingsList } from "../-bookings/BookingsList";
import { BookingStatusTabs, type BookingsRouteSearch } from "../-bookings/BookingStatusTabs";
import {
  type BookingListItem,
  type BookingStatusView,
  getBookingStatusLabel,
  isBookingStatusView
} from "../-bookings/bookingTypes";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";

const pageSize = 25;

export const Route = createFileRoute("/bookings/$status")({
  staticData: { trackingTitle: "Bookings" },
  validateSearch: (search: Record<string, unknown>) => ({
    search: stringValue(search.search),
    eventTypeId: stringValue(search.eventTypeId),
    attendeeName: stringValue(search.attendeeName),
    attendeeEmail: stringValue(search.attendeeEmail),
    bookingUid: stringValue(search.bookingUid),
    dateFrom: stringValue(search.dateFrom),
    dateTo: stringValue(search.dateTo),
    pageOffset: numberValue(search.pageOffset) ?? 0
  }),
  component: BookingsPage
});

function BookingsPage() {
  const { status: rawStatus } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const status: BookingStatusView = isBookingStatusView(rawStatus) ? rawStatus : "upcoming";
  const [selectedBooking, setSelectedBooking] = useState<BookingListItem | null>(null);
  const { data: eventTypesData } = api.useQuery("get", "/api/event-types");
  const { data: bookingsData, isLoading } = api.useQuery("get", "/api/bookings", {
    params: {
      query: {
        Status: status,
        Search: search.search,
        EventTypeId: search.eventTypeId,
        AttendeeName: search.attendeeName,
        AttendeeEmail: search.attendeeEmail,
        BookingUid: search.bookingUid,
        AfterStartDate: toDateTimeOffset(search.dateFrom, false),
        BeforeEndDate: toDateTimeOffset(search.dateTo, true),
        PageOffset: search.pageOffset,
        PageSize: pageSize
      }
    }
  });

  useEffect(() => {
    if (!isBookingStatusView(rawStatus)) {
      navigate({ to: "/bookings/$status", params: { status: "upcoming" }, search, replace: true });
    }
  }, [navigate, rawStatus, search]);

  const updateSearch = (nextSearch: BookingFilterSearch) => {
    navigate({ search: toRouteSearch(nextSearch), replace: true });
  };

  const totalCount = bookingsData?.totalCount ?? 0;
  const pageOffset = bookingsData?.pageOffset ?? search.pageOffset;
  const canGoPrevious = pageOffset > 0;
  const canGoNext = pageOffset + pageSize < totalCount;

  return (
    <SchedulingPageShell
      title={t`Bookings`}
      subtitle={t`View and manage appointments clients have booked.`}
      maxWidth="80rem"
    >
      <div className="flex flex-col gap-4">
        <BookingStatusTabs status={status} search={search} />
        <BookingsFilters eventTypes={eventTypesData?.eventTypes ?? []} search={search} onSearchChange={updateSearch} />
        <div className="flex flex-wrap items-center justify-between gap-3">
          <span className="text-sm text-muted-foreground">
            <Trans>
              {totalCount} {getBookingStatusLabel(status).toLowerCase()} bookings
            </Trans>
          </span>
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="secondary"
              disabled={!canGoPrevious}
              onClick={() => navigate({ search: { ...search, pageOffset: Math.max(0, pageOffset - pageSize) } })}
            >
              <Trans>Previous</Trans>
            </Button>
            <Button
              type="button"
              variant="secondary"
              disabled={!canGoNext}
              onClick={() => navigate({ search: { ...search, pageOffset: pageOffset + pageSize } })}
            >
              <Trans>Next</Trans>
            </Button>
          </div>
        </div>
        <BookingsList
          bookings={bookingsData?.bookings ?? []}
          status={status}
          isLoading={isLoading}
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
    </SchedulingPageShell>
  );
}

function stringValue(value: unknown) {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function toRouteSearch(search: BookingFilterSearch, pageOffset = 0): BookingsRouteSearch {
  return {
    search: search.search,
    eventTypeId: search.eventTypeId,
    attendeeName: search.attendeeName,
    attendeeEmail: search.attendeeEmail,
    bookingUid: search.bookingUid,
    dateFrom: search.dateFrom,
    dateTo: search.dateTo,
    pageOffset
  };
}

function numberValue(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return Math.max(0, Math.floor(value));
  if (typeof value !== "string" || value.trim().length === 0) return undefined;

  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.max(0, Math.floor(parsed)) : undefined;
}

function toDateTimeOffset(value: string | undefined, endOfDay: boolean) {
  if (!value) return undefined;

  const suffix = endOfDay ? "T23:59:59.999" : "T00:00:00.000";
  return new Date(`${value}${suffix}`).toISOString();
}

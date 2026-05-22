import { t } from "@lingui/core/macro";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";

import { api } from "@/shared/lib/api/client";

import { BookingCalendarContainer } from "../-bookings/BookingCalendarContainer";
import { BookingListContainer } from "../-bookings/BookingListContainer";
import { type BookingFilterSearch } from "../-bookings/BookingsFilters";
import { type BookingsRouteSearch } from "../-bookings/BookingStatusTabs";
import {
  type BookingDashboardView,
  type BookingStatusView,
  getWeekStartDate,
  isBookingDashboardView,
  isBookingStatusView
} from "../-bookings/bookingTypes";
import { getStoredBookingsView, useBookingsView } from "../-bookings/useBookingsView";
import { formatWeekStartSearchValue } from "../-bookings/WeekPicker";
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
    noShowOnly: booleanValue(search.noShowOnly),
    hasInternalNote: booleanValue(search.hasInternalNote),
    minRating: ratingValue(search.minRating),
    view: stringValue(search.view),
    weekStart: stringValue(search.weekStart),
    pageOffset: numberValue(search.pageOffset) ?? 0
  }),
  component: BookingsPage
});

function BookingsPage() {
  const { status: rawStatus } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const status: BookingStatusView = isBookingStatusView(rawStatus) ? rawStatus : "upcoming";
  const initialView: BookingDashboardView = isBookingDashboardView(search.view) ? search.view : getStoredBookingsView();
  const [view, setView] = useBookingsView({
    view: initialView,
    onViewChange: (nextView) => navigate({ search: { ...search, view: nextView }, replace: true })
  });
  const { data: eventTypesData } = api.useQuery("get", "/api/event-types");
  const { data: bookingsData, isLoading } = api.useQuery(
    "get",
    "/api/bookings",
    {
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
          NoShowOnly: search.noShowOnly,
          HasInternalNote: search.hasInternalNote,
          MinRating: search.minRating,
          PageOffset: search.pageOffset,
          PageSize: pageSize
        }
      }
    },
    { enabled: view === "list" }
  );

  useEffect(() => {
    if (!isBookingStatusView(rawStatus)) {
      navigate({ to: "/bookings/$status", params: { status: "upcoming" }, search, replace: true });
    }
  }, [navigate, rawStatus, search]);

  useEffect(() => {
    if (!isBookingDashboardView(search.view)) {
      navigate({ search: { ...search, view }, replace: true });
    }
  }, [navigate, search, view]);

  const updateSearch = (nextSearch: BookingFilterSearch) => {
    navigate({ search: toRouteSearch(nextSearch, view, search.weekStart), replace: true });
  };

  const updatePageOffset = (pageOffset: number) => {
    navigate({ search: { ...search, pageOffset }, replace: true });
  };

  const updateWeekStart = (weekStart: Date) => {
    navigate({ search: { ...search, weekStart: formatWeekStartSearchValue(weekStart) }, replace: true });
  };

  return (
    <SchedulingPageShell
      title={t`Bookings`}
      subtitle={t`View and manage appointments clients have booked.`}
      maxWidth="80rem"
    >
      {view === "list" ? (
        <BookingListContainer
          status={status}
          search={normalizeSearch(search, view)}
          eventTypes={eventTypesData?.eventTypes ?? []}
          bookings={bookingsData?.bookings ?? []}
          totalCount={bookingsData?.totalCount ?? 0}
          pageSize={pageSize}
          isLoading={isLoading}
          view={view}
          onViewChange={setView}
          onSearchChange={updateSearch}
          onPageOffsetChange={updatePageOffset}
        />
      ) : (
        <BookingCalendarContainer
          status={status}
          search={normalizeSearch(search, view)}
          eventTypes={eventTypesData?.eventTypes ?? []}
          view={view}
          onViewChange={setView}
          onSearchChange={updateSearch}
          onWeekStartChange={updateWeekStart}
        />
      )}
    </SchedulingPageShell>
  );
}

function stringValue(value: unknown) {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : undefined;
}

function toRouteSearch(
  search: BookingFilterSearch,
  view: BookingDashboardView,
  weekStart: string | undefined,
  pageOffset = 0
): BookingsRouteSearch {
  return {
    search: search.search,
    eventTypeId: search.eventTypeId,
    attendeeName: search.attendeeName,
    attendeeEmail: search.attendeeEmail,
    bookingUid: search.bookingUid,
    dateFrom: search.dateFrom,
    dateTo: search.dateTo,
    noShowOnly: search.noShowOnly,
    hasInternalNote: search.hasInternalNote,
    minRating: search.minRating,
    view,
    weekStart: weekStart ?? formatWeekStartSearchValue(getWeekStartDate(new Date())),
    pageOffset
  };
}

function normalizeSearch(search: ReturnType<typeof Route.useSearch>, view: BookingDashboardView): BookingsRouteSearch {
  return {
    search: search.search,
    eventTypeId: search.eventTypeId,
    attendeeName: search.attendeeName,
    attendeeEmail: search.attendeeEmail,
    bookingUid: search.bookingUid,
    dateFrom: search.dateFrom,
    dateTo: search.dateTo,
    noShowOnly: search.noShowOnly,
    hasInternalNote: search.hasInternalNote,
    minRating: search.minRating,
    view,
    weekStart: search.weekStart ?? formatWeekStartSearchValue(getWeekStartDate(new Date())),
    pageOffset: search.pageOffset
  };
}

function numberValue(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return Math.max(0, Math.floor(value));
  if (typeof value !== "string" || value.trim().length === 0) return undefined;

  const parsed = Number(value);
  return Number.isFinite(parsed) ? Math.max(0, Math.floor(parsed)) : undefined;
}

function booleanValue(value: unknown): boolean | undefined {
  if (typeof value === "boolean") return value || undefined;
  if (value === "true") return true;
  return undefined;
}

function ratingValue(value: unknown) {
  const parsed = numberValue(value);
  if (parsed === undefined) return undefined;
  return Math.min(5, Math.max(1, parsed));
}

function toDateTimeOffset(value: string | undefined, endOfDay: boolean) {
  if (!value) return undefined;

  const suffix = endOfDay ? "T23:59:59.999" : "T00:00:00.000";
  return new Date(`${value}${suffix}`).toISOString();
}

import type React from "react";

/* eslint-disable max-lines */
import { Alert, AlertDescription, AlertTitle } from "@repo/ui/components/Alert";
import { Button } from "@repo/ui/components/Button";
import { ButtonGroup } from "@repo/ui/components/ButtonGroup";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { createFileRoute } from "@tanstack/react-router";
import { ArrowLeftIcon, BookmarkIcon, ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { PublicBooker } from "./$handle/-booker/PublicBooker";
import {
  formatDateOnly,
  type AvailableSlot,
  type PublicEventType,
  type PublicRescheduleBooking,
  type PublicSlot
} from "./$handle/-booker/publicBookerTypes";
import { ActiveBookingFilters } from "./-bookings/ActiveBookingFilters";
import { BookingCalendarView } from "./-bookings/BookingCalendarView";
import { BookingDetailsSheet } from "./-bookings/BookingDetailsSheet";
import { BookingListContainer } from "./-bookings/BookingListContainer";
import { BookingsFilters } from "./-bookings/BookingsFilters";
import { BookingStatusTabs, type BookingsRouteSearch } from "./-bookings/BookingStatusTabs";
import {
  type BookingDashboardView,
  type BookingListItem,
  type BookingStatusView,
  getWeekStartDate
} from "./-bookings/bookingTypes";
import { BookingViewToggleButton } from "./-bookings/BookingViewToggleButton";
import { WeekPicker } from "./-bookings/WeekPicker";
import { EventTypeEditorTabs } from "./-scheduling/event-types-shell/EventTypeEditorTabs";
import { EventTypeHeaderActions } from "./-scheduling/event-types-shell/EventTypeHeaderActions";
import { type EventTypeTabName } from "./-scheduling/event-types-shell/eventTypeShellTypes";
import {
  eventTypeToPayload,
  type EventType,
  type EventTypePayload,
  type Schedule
} from "./-scheduling/schedulingTypes";

type PublicBookerVisualState =
  | "unavailable"
  | "selecting-date"
  | "selecting-time"
  | "booking-form"
  | "reschedule-form"
  | "success";

type EventTypeEditorVisualState = Extract<
  EventTypeTabName,
  "setup" | "availability" | "limits" | "advanced" | "workflows" | "webhooks"
>;

type BookingDashboardVisualState =
  | "list-upcoming"
  | "list-unconfirmed"
  | "list-past"
  | "list-cancelled"
  | "list-empty"
  | "list-loading"
  | "list-error"
  | "calendar-week"
  | "calendar-empty"
  | "calendar-selected"
  | "details-info"
  | "details-actions"
  | "details-reschedule";

export const Route = createFileRoute("/cal-com-ui-parity")({
  staticData: { trackingTitle: "Cal.com UI parity fixture" },
  validateSearch: (search: Record<string, unknown>) => ({
    surface: normalizeSurface(search.surface),
    state: normalizeVisualState(search.surface, search.state)
  }),
  component: CalComUiParityFixture
});

function CalComUiParityFixture() {
  const { surface, state } = Route.useSearch();

  return (
    <main
      className="min-h-screen bg-[#f7f7f7] px-4 py-10 text-foreground sm:px-6 lg:px-8"
      data-testid="cal-com-ui-parity-fixture"
    >
      {surface === "public-booker" ? (
        <PublicBookerFixture state={isPublicBookerVisualState(state) ? state : "selecting-time"} />
      ) : surface === "booking-dashboard" ? (
        <BookingDashboardFixture state={isBookingDashboardVisualState(state) ? state : "list-upcoming"} />
      ) : (
        <EventTypeEditorFixture state={isEventTypeEditorVisualState(state) ? state : "setup"} />
      )}
    </main>
  );
}

function PublicBookerFixture({ state }: Readonly<{ state: PublicBookerVisualState }>) {
  const fixedDate = useMemo(() => new Date("2026-06-15T00:00:00.000"), []);
  const fixedSlot = "2026-06-15T09:00:00.000Z";
  const [selectedDate, setSelectedDate] = useState<Date | null>(
    state === "selecting-date" || state === "unavailable" ? null : fixedDate
  );
  const [selectedSlot, setSelectedSlot] = useState<string | null>(
    state === "booking-form" || state === "reschedule-form" || state === "success" ? fixedSlot : null
  );
  const eventType = state === "unavailable" ? null : visualEventType;
  const rescheduleBooking = state === "reschedule-form" ? visualRescheduleBooking : null;

  return (
    <PublicBooker
      handle="visual"
      eventSlug="product-consultation"
      eventType={eventType}
      rescheduleBooking={rescheduleBooking}
      rescheduleUnavailable={false}
      slotsByDate={visualSlots}
      isLoading={false}
      selectedDate={selectedDate}
      selectedSlot={selectedSlot}
      selectedDuration={30}
      timezone="Africa/Johannesburg"
      privateLink={undefined}
      rescheduledBy="owner@example.com"
      monthAnchor={fixedDate}
      onDateChange={(date) => {
        setSelectedDate(date);
        setSelectedSlot(null);
      }}
      onMonthChange={() => {}}
      onSlotChange={(slot: AvailableSlot) => setSelectedSlot(slot.value)}
      onTimezoneChange={() => {}}
      onBookingComplete={() => {}}
      onBackToDates={() => {
        setSelectedDate(null);
        setSelectedSlot(null);
      }}
      onBackToTimes={() => setSelectedSlot(null)}
    />
  );
}

function isPublicBookerVisualState(value: unknown): value is PublicBookerVisualState {
  return (
    value === "unavailable" ||
    value === "selecting-date" ||
    value === "selecting-time" ||
    value === "booking-form" ||
    value === "reschedule-form" ||
    value === "success"
  );
}

function isEventTypeEditorVisualState(value: unknown): value is EventTypeEditorVisualState {
  return (
    value === "setup" ||
    value === "availability" ||
    value === "limits" ||
    value === "advanced" ||
    value === "workflows" ||
    value === "webhooks"
  );
}

function isBookingDashboardVisualState(value: unknown): value is BookingDashboardVisualState {
  return (
    value === "list-upcoming" ||
    value === "list-unconfirmed" ||
    value === "list-past" ||
    value === "list-cancelled" ||
    value === "list-empty" ||
    value === "list-loading" ||
    value === "list-error" ||
    value === "calendar-week" ||
    value === "calendar-empty" ||
    value === "calendar-selected" ||
    value === "details-info" ||
    value === "details-actions" ||
    value === "details-reschedule"
  );
}

function normalizeSurface(value: unknown) {
  if (value === "event-type-editor" || value === "booking-dashboard") return value;
  return "public-booker";
}

function normalizeVisualState(surface: unknown, state: unknown) {
  if (surface === "event-type-editor") {
    return isEventTypeEditorVisualState(state) ? state : "setup";
  }

  if (surface === "booking-dashboard") {
    return isBookingDashboardVisualState(state) ? state : "list-upcoming";
  }

  return isPublicBookerVisualState(state) ? state : "selecting-time";
}

function BookingDashboardFixture({ state }: Readonly<{ state: BookingDashboardVisualState }>) {
  const [view, setView] = useState<BookingDashboardView>(state.startsWith("calendar") ? "calendar" : "list");
  const [search, setSearch] = useState<BookingsRouteSearch>(visualBookingsSearch);
  const weekStart = getWeekStartDate(new Date("2026-06-15T00:00:00.000"));
  const selectedBooking = state === "details-reschedule" ? visualCancelledBookings[0] : visualUpcomingBookings[0];

  if (state === "list-error") {
    return (
      <section className="mx-auto max-w-[80rem]" data-testid="booking-dashboard-fixture">
        <BookingDashboardToolbar
          status="upcoming"
          search={search}
          view={view}
          onViewChange={setView}
          onSearchChange={(nextSearch) => setSearch((current) => ({ ...current, ...nextSearch }))}
          weekStart={weekStart}
          onWeekStartChange={() => {}}
        />
        <Alert variant="destructive" className="mt-4 rounded-2xl">
          <AlertTitle>Something went wrong</AlertTitle>
          <AlertDescription>Bookings could not be loaded. Try refreshing the page.</AlertDescription>
        </Alert>
      </section>
    );
  }

  if (state === "calendar-week" || state === "calendar-empty" || state === "calendar-selected") {
    return <BookingCalendarDashboardFixture state={state} search={search} setSearch={setSearch} />;
  }

  if (state === "details-info" || state === "details-actions" || state === "details-reschedule") {
    return (
      <section className="mx-auto max-w-[80rem]" data-testid="booking-dashboard-fixture">
        <BookingListContainer
          status="upcoming"
          search={visualBookingsSearch}
          eventTypes={[visualEditorEventType]}
          bookings={visualUpcomingBookings}
          totalCount={visualUpcomingBookings.length}
          pageSize={25}
          isLoading={false}
          view="list"
          onViewChange={() => {}}
          onSearchChange={() => {}}
          onPageOffsetChange={() => {}}
        />
        <BookingDetailsSheet booking={selectedBooking} isOpen={true} onOpenChange={() => {}} />
      </section>
    );
  }

  const status = getVisualBookingStatus(state);
  const bookings = getVisualBookingsForState(state);

  return (
    <section className="mx-auto max-w-[80rem]" data-testid="booking-dashboard-fixture">
      <BookingListContainer
        status={status}
        search={visualBookingsSearch}
        eventTypes={[visualEditorEventType]}
        bookings={bookings}
        totalCount={bookings.length}
        pageSize={25}
        isLoading={state === "list-loading"}
        view="list"
        onViewChange={setView}
        onSearchChange={(nextSearch) => setSearch((current) => ({ ...current, ...nextSearch }))}
        onPageOffsetChange={(pageOffset) => setSearch((current) => ({ ...current, pageOffset }))}
      />
    </section>
  );
}

function BookingCalendarDashboardFixture({
  state,
  search,
  setSearch
}: Readonly<{
  state: Extract<BookingDashboardVisualState, "calendar-week" | "calendar-empty" | "calendar-selected">;
  search: BookingsRouteSearch;
  setSearch: React.Dispatch<React.SetStateAction<BookingsRouteSearch>>;
}>) {
  const weekStart = getWeekStartDate(new Date("2026-06-15T00:00:00.000"));
  const bookings = state === "calendar-empty" ? [] : visualCalendarBookings;
  const [selectedCalendarBooking, setSelectedCalendarBooking] = useState<BookingListItem | null>(
    state === "calendar-selected" ? bookings[0] : null
  );

  return (
    <section className="mx-auto max-w-[80rem]" data-testid="booking-dashboard-fixture">
      <BookingDashboardToolbar
        status="upcoming"
        search={{ ...search, view: "calendar" }}
        view="calendar"
        onViewChange={() => {}}
        onSearchChange={(nextSearch) => setSearch((current) => ({ ...current, ...nextSearch }))}
        weekStart={weekStart}
        onWeekStartChange={() => {}}
      />
      <div className="mt-4">
        <BookingCalendarView
          bookings={bookings}
          weekStart={weekStart}
          isLoading={false}
          selectedBookingId={selectedCalendarBooking?.id ?? null}
          onSelectBooking={setSelectedCalendarBooking}
        />
      </div>
      <BookingDetailsSheet
        booking={selectedCalendarBooking}
        isOpen={selectedCalendarBooking !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) setSelectedCalendarBooking(null);
        }}
      />
    </section>
  );
}

function BookingDashboardToolbar({
  status,
  search,
  view,
  weekStart,
  onViewChange,
  onSearchChange,
  onWeekStartChange
}: Readonly<{
  status: BookingStatusView;
  search: BookingsRouteSearch;
  view: BookingDashboardView;
  weekStart: Date;
  onViewChange: (view: BookingDashboardView) => void;
  onSearchChange: (search: Partial<BookingsRouteSearch>) => void;
  onWeekStartChange: (weekStart: Date) => void;
}>) {
  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <div className="w-full md:w-auto">
            <BookingStatusTabs status={status} search={search} />
          </div>
          {view === "calendar" ? <WeekPicker weekStart={weekStart} onWeekStartChange={onWeekStartChange} /> : null}
          <BookingsFilters
            eventTypes={[visualEditorEventType]}
            search={search}
            onSearchChange={(nextSearch) => onSearchChange(nextSearch)}
          />
        </div>
        <div className="flex items-center gap-2">
          <Button type="button" variant="secondary" size="sm" disabled>
            <BookmarkIcon />
            Saved segments
          </Button>
          {view === "calendar" ? (
            <>
              <Button type="button" variant="secondary" size="sm" onClick={() => onWeekStartChange(weekStart)}>
                Today
              </Button>
              <ButtonGroup>
                <Tooltip>
                  <TooltipTrigger
                    render={
                      <Button type="button" variant="secondary" size="icon-sm" aria-label="View previous week">
                        <ChevronLeftIcon />
                      </Button>
                    }
                  />
                  <TooltipContent>View previous week</TooltipContent>
                </Tooltip>
                <Tooltip>
                  <TooltipTrigger
                    render={
                      <Button type="button" variant="secondary" size="icon-sm" aria-label="View next week">
                        <ChevronRightIcon />
                      </Button>
                    }
                  />
                  <TooltipContent>View next week</TooltipContent>
                </Tooltip>
              </ButtonGroup>
            </>
          ) : null}
          <BookingViewToggleButton view={view} onViewChange={onViewChange} />
        </div>
      </div>
      <ActiveBookingFilters
        eventTypes={[visualEditorEventType]}
        search={search}
        onSearchChange={(nextSearch) => onSearchChange(nextSearch)}
      />
    </>
  );
}

function getVisualBookingStatus(state: BookingDashboardVisualState): BookingStatusView {
  if (state === "list-unconfirmed") return "unconfirmed";
  if (state === "list-past") return "past";
  if (state === "list-cancelled") return "cancelled";
  return "upcoming";
}

function getVisualBookingsForState(state: BookingDashboardVisualState) {
  if (state === "list-empty") return [];
  if (state === "list-unconfirmed") return visualUnconfirmedBookings;
  if (state === "list-past") return visualPastBookings;
  if (state === "list-cancelled") return visualCancelledBookings;
  return visualUpcomingBookings;
}

function EventTypeEditorFixture({ state }: Readonly<{ state: EventTypeEditorVisualState }>) {
  const [draft, setDraft] = useState<EventTypePayload>(() => ({
    ...eventTypeToPayload(visualEditorEventType),
    description:
      "A focused product consultation to review goals, constraints, and next actions. This field is intentionally edited for the dirty save state."
  }));

  return (
    <section className="mx-auto max-w-[80rem]" data-testid="event-type-layout">
      <div className="mb-6 flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex min-w-0 items-center gap-3">
            <Button type="button" variant="ghost" size="icon-sm" aria-label="Back">
              <ArrowLeftIcon />
            </Button>
            <h1 className="truncate text-2xl font-semibold tracking-normal">{visualEditorEventType.title}</h1>
          </div>
          <p className="mt-2 pl-11 text-sm text-muted-foreground">/visual/product-consultation</p>
        </div>
        <EventTypeHeaderActions
          eventType={visualEditorEventType}
          draft={draft}
          publicHandle="visual"
          canSave={true}
          isSaving={false}
          onDraftChange={setDraft}
          onDelete={() => {}}
        />
      </div>
      <EventTypeEditorTabs
        eventTypeId={visualEditorEventType.id}
        tabName={state}
        draft={draft}
        schedules={visualSchedules}
        canSave={true}
        onChange={setDraft}
        onSubmit={() => {}}
      />
    </section>
  );
}

const visualEventType = {
  afterEventBufferMinutes: 0,
  beforeEventBufferMinutes: 0,
  bookingFields: [],
  bookingWindow: { fixedEndDate: null, fixedStartDate: null, rollingWindowDays: 30 },
  confirmationPolicy: { requiresBookerEmailVerification: false, requiresConfirmation: false },
  description: "A focused product consultation to review goals, constraints, and next actions.",
  durationMinutes: 30,
  durationOptions: [30, 45, 60],
  handle: "visual",
  locations: [{ type: "link", value: "Cal Video" }],
  locationType: "link",
  locationValue: "Cal Video",
  minimumBookingNoticeMinutes: 60,
  profile: { avatarUrl: null, displayName: "Nerova Product" },
  recurrence: null,
  seats: { capacity: null, enabled: false, showAttendeeInfo: false },
  slotIntervalMinutes: 30,
  slug: "product-consultation",
  title: "Product Consultation"
} satisfies PublicEventType;

const visualEventTypeSettings = {
  bookerLayout: "month",
  bookingFields: [
    {
      defaultLabel: "Company",
      defaultPlaceholder: null,
      disableOnPrefill: false,
      editable: "system",
      excludeEmails: null,
      getOptionsAt: null,
      hidden: false,
      hideWhenJustOneOption: false,
      label: "Company",
      labelAsSafeHtml: null,
      maxLength: 120,
      minLength: null,
      name: "company",
      options: [],
      optionsInputs: {},
      placeholder: "Acme Inc.",
      price: null,
      required: true,
      requireEmails: null,
      sources: [],
      type: "text",
      variant: null,
      variantsConfig: null,
      views: []
    }
  ],
  bookingWindow: { fixedEndDate: null, fixedStartDate: null, rollingWindowDays: 30 },
  cancellationPolicy: { allowCancellation: true, minimumNoticeMinutes: 120 },
  confirmationPolicy: { requiresBookerEmailVerification: false, requiresConfirmation: true },
  durationOptions: [30, 45, 60],
  eventColor: "#292929",
  interfaceLanguage: "en",
  limits: {
    firstAvailableSlotMinutes: 0,
    maxActiveBookingsPerBooker: 2,
    maxBookingDurationMinutesPerDay: 120,
    maxBookingsPerDay: 6,
    offsetStartMinutes: 0
  },
  locations: [{ type: "link", value: "Cal Video" }],
  metadata: {},
  privateLinks: [{ expiresAt: "2026-07-01T00:00:00.000Z", link: "vip", maxUsageCount: 10, usageCount: 2 }],
  recurrence: null,
  redirects: { cancellationUrl: null, successUrl: "https://example.com/thanks" },
  reschedulePolicy: { allowReschedule: true, minimumNoticeMinutes: 180 },
  seats: { capacity: 4, enabled: true, showAttendeeInfo: true },
  selectedCalendars: [{ credentialId: null, externalId: "primary", integration: "google-calendar" }]
} satisfies EventType["settings"];

const visualEditorEventType = {
  afterEventBufferMinutes: 10,
  beforeEventBufferMinutes: 10,
  description: "A focused product consultation to review goals, constraints, and next actions.",
  durationMinutes: 30,
  hidden: false,
  id: "etype_visual_product_consultation",
  locationType: "link",
  locationValue: "Cal Video",
  minimumBookingNoticeMinutes: 60,
  scheduleId: "sch_visual_default",
  settings: visualEventTypeSettings,
  slotIntervalMinutes: 30,
  slug: "product-consultation",
  title: "Product Consultation"
} satisfies EventType;

const visualSchedules = [
  {
    availabilityWindows: [{ days: [1, 2, 3, 4, 5], endMinute: 1020, startMinute: 540 }],
    dateOverrides: [],
    id: "sch_visual_default",
    isDefault: true,
    name: "Working hours",
    timeZone: "Africa/Johannesburg"
  }
] satisfies Schedule[];

const visualBookingsSearch = {
  attendeeEmail: undefined,
  attendeeName: undefined,
  bookingUid: undefined,
  dateFrom: undefined,
  dateTo: undefined,
  eventTypeId: undefined,
  pageOffset: 0,
  search: undefined,
  view: "list",
  weekStart: "2026-06-15"
} satisfies BookingsRouteSearch;

const visualUpcomingBookings = [
  visualBooking({
    id: "book_visual_strategy",
    bookerEmail: "maria.lopez@example.com",
    bookerName: "Maria Lopez",
    description: "We want to review launch sequencing and integration risk.",
    endTime: "2026-06-15T09:30:00.000Z",
    responses: {
      Company: "Acme Inc.",
      "Main goal": "Reduce scheduling handoffs before launch."
    },
    startTime: "2026-06-15T09:00:00.000Z"
  }),
  visualBooking({
    id: "book_visual_enterprise",
    bookerEmail: "david.chen@example.com",
    bookerName: "David Chen",
    description: "Enterprise buyer review with technical stakeholders.",
    endTime: "2026-06-16T13:45:00.000Z",
    startTime: "2026-06-16T13:00:00.000Z"
  }),
  visualBooking({
    id: "book_visual_success",
    bookerEmail: "amina.patel@example.com",
    bookerName: "Amina Patel",
    endTime: "2026-06-18T15:30:00.000Z",
    isRecurring: true,
    startTime: "2026-06-18T15:00:00.000Z"
  })
];

const visualUnconfirmedBookings = [
  visualBooking({
    id: "book_visual_pending",
    bookerEmail: "sam.green@example.com",
    bookerName: "Sam Green",
    endTime: "2026-06-17T11:30:00.000Z",
    listingStatus: "unconfirmed",
    startTime: "2026-06-17T11:00:00.000Z",
    status: "pending"
  })
];

const visualPastBookings = [
  visualBooking({
    id: "book_visual_past",
    bookerEmail: "jordan.reed@example.com",
    bookerName: "Jordan Reed",
    endTime: "2026-06-09T10:30:00.000Z",
    listingStatus: "past",
    startTime: "2026-06-09T10:00:00.000Z"
  })
];

const visualCancelledBookings = [
  visualBooking({
    id: "book_visual_cancelled",
    bookerEmail: "nora.smith@example.com",
    bookerName: "Nora Smith",
    cancellationReason: "Booker requested a later time after internal planning changed.",
    cancelledBy: "owner@example.com",
    endTime: "2026-06-19T12:30:00.000Z",
    listingStatus: "cancelled",
    rescheduleReason: "Moving to the following week.",
    rescheduled: true,
    rescheduledBy: "owner@example.com",
    startTime: "2026-06-19T12:00:00.000Z",
    status: "cancelled"
  })
];

const visualCalendarBookings = [...visualUpcomingBookings, ...visualUnconfirmedBookings, ...visualPastBookings];

function visualBooking({
  id,
  bookerEmail,
  bookerName,
  cancellationReason = null,
  cancelledBy = null,
  description = null,
  endTime,
  isRecurring = false,
  listingStatus = "upcoming",
  rejectionReason = null,
  rescheduleReason = null,
  rescheduled = false,
  rescheduledBy = null,
  responses = {},
  startTime,
  status = "accepted"
}: {
  id: string;
  bookerEmail: string;
  bookerName: string;
  cancellationReason?: string | null;
  cancelledBy?: string | null;
  description?: string | null;
  endTime: string;
  isRecurring?: boolean;
  listingStatus?: string;
  rejectionReason?: string | null;
  rescheduleReason?: string | null;
  rescheduled?: boolean;
  rescheduledBy?: string | null;
  responses?: Record<string, string>;
  startTime: string;
  status?: string;
}): BookingListItem {
  return {
    actions: visualBookingActions(status),
    attendees: [
      {
        email: "guest@example.com",
        locale: null,
        name: "Guest Reviewer",
        noShow: false,
        phoneNumber: null,
        timeZone: "Africa/Johannesburg"
      }
    ],
    bookerEmail,
    bookerName,
    cancellationReason,
    cancelledBy,
    createdAt: "2026-06-01T08:00:00.000Z",
    description,
    endTime,
    eventTypeId: visualEditorEventType.id,
    eventTypeSlug: visualEditorEventType.slug,
    eventTypeTitle: visualEditorEventType.title,
    id,
    isRecurring,
    listingStatus,
    locationType: "link",
    locationValue: "https://cal.video/visual-product",
    locations: [{ type: "link", value: "https://cal.video/visual-product" }],
    metadata: { source: "cal-com-ui-parity" },
    references: [],
    rejectionReason,
    rescheduleReason,
    rescheduled,
    rescheduledBy,
    responses,
    schedulingHandle: "visual",
    seatReferences: [],
    startTime,
    status,
    timeZone: "Africa/Johannesburg"
  } as BookingListItem;
}

function visualBookingActions(status: string) {
  const isPending = status === "pending";
  const isCancelled = status === "cancelled" || status === "rejected";
  const enabledMutable = !isCancelled;
  const action = (visible: boolean, enabled: boolean, disabledReason: string | null = null) => ({
    disabledReason,
    enabled,
    visible
  });

  return {
    addGuests: action(true, enabledMutable, enabledMutable ? null : "Cancelled bookings cannot add guests."),
    cancel: action(true, enabledMutable, enabledMutable ? null : "Cancelled bookings cannot be cancelled."),
    confirm: action(isPending, isPending),
    editLocation: action(true, enabledMutable, enabledMutable ? null : "Cancelled bookings cannot change location."),
    markNoShow: action(true, false, "No-show tracking is not implemented yet."),
    reject: action(isPending, isPending),
    report: action(true, false, "Report booking is not implemented yet."),
    requestReschedule: action(
      true,
      enabledMutable,
      enabledMutable ? null : "Cancelled bookings cannot be rescheduled."
    ),
    reschedule: action(true, enabledMutable, enabledMutable ? null : "Cancelled bookings cannot be rescheduled."),
    viewRecordings: action(true, false, "Recordings are not available until conferencing is ported."),
    viewSessionDetails: action(true, false, "Session details are not available until conferencing is ported.")
  };
}

const visualRescheduleBooking = {
  bookerEmail: "visual-booker@example.com",
  bookerName: "Visual Booker",
  canReschedule: true,
  disabledReason: null,
  endTime: "2026-06-08T09:30:00.000Z",
  eventSlug: "product-consultation",
  handle: "visual",
  id: "book_visual_original",
  responses: { notes: "Original discovery notes." },
  startTime: "2026-06-08T09:00:00.000Z",
  status: "accepted",
  timeZone: "Africa/Johannesburg"
} satisfies PublicRescheduleBooking;

const visualSlots = {
  [formatDateOnly(new Date("2026-06-15T00:00:00.000"))]: [
    slot("2026-06-15T09:00:00.000Z"),
    slot("2026-06-15T09:30:00.000Z"),
    slot("2026-06-15T10:00:00.000Z"),
    slot("2026-06-15T11:00:00.000Z"),
    slot("2026-06-15T13:30:00.000Z"),
    slot("2026-06-15T15:00:00.000Z")
  ]
} satisfies Record<string, PublicSlot[]>;

function slot(time: string): PublicSlot {
  const start = new Date(time);
  const end = new Date(start);
  end.setMinutes(end.getMinutes() + 30);
  return { attendees: null, bookingUid: null, endTime: end.toISOString(), time };
}

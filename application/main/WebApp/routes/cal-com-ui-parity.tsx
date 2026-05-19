import { createFileRoute } from "@tanstack/react-router";
import { ArrowLeftIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { Button } from "@repo/ui/components/Button";

import { EventTypeEditorTabs } from "./-scheduling/event-types-shell/EventTypeEditorTabs";
import { EventTypeHeaderActions } from "./-scheduling/event-types-shell/EventTypeHeaderActions";
import { isEventTypeTabName, type EventTypeTabName } from "./-scheduling/event-types-shell/eventTypeShellTypes";
import {
  eventTypeToPayload,
  type EventType,
  type EventTypePayload,
  type Schedule
} from "./-scheduling/schedulingTypes";
import { PublicBooker } from "./$handle/-booker/PublicBooker";
import {
  formatDateOnly,
  type AvailableSlot,
  type PublicEventType,
  type PublicRescheduleBooking,
  type PublicSlot
} from "./$handle/-booker/publicBookerTypes";

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

export const Route = createFileRoute("/cal-com-ui-parity")({
  staticData: { trackingTitle: "Cal.com UI parity fixture" },
  validateSearch: (search: Record<string, unknown>) => ({
    surface: search.surface === "event-type-editor" ? "event-type-editor" : "public-booker",
    state:
      search.surface === "event-type-editor"
        ? isEventTypeEditorVisualState(search.state)
          ? search.state
          : "setup"
        : isPublicBookerVisualState(search.state)
          ? search.state
          : "selecting-time"
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
      <div className="lg:hidden">
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

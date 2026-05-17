import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Calendar } from "@repo/ui/components/Calendar";
import { Form } from "@repo/ui/components/Form";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { TimeZonePicker } from "@repo/ui/components/TimeZonePicker";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { CalendarIcon, CheckIcon, ClockIcon, GlobeIcon, MapPinIcon, UserIcon, XIcon } from "lucide-react";
import { useMemo } from "react";

import { api, type Schemas } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { formatMinutes } from "../-scheduling/schedulingTypes";

type BookerState = "loading" | "selecting_date" | "selecting_time" | "booking" | "confirmed";
type PublicEventType = Schemas["PublicEventTypeResponse"];
type PublicSlot = Schemas["PublicSlotResponse"];
type AvailableSlot = PublicSlot & { startsAt: Date; label: string; value: string };

export const Route = createFileRoute("/$handle/$eventSlug")({
  staticData: { trackingTitle: "Public booker" },
  validateSearch: (search: Record<string, unknown>) => ({
    month: stringValue(search.month),
    date: stringValue(search.date),
    slot: stringValue(search.slot),
    duration: numberValue(search.duration),
    timezone: stringValue(search.timezone) ?? stringValue(search["cal.tz"]),
    privateLink: stringValue(search.privateLink)
  }),
  component: PublicBookerWebWrapper
});

function PublicBookerWebWrapper() {
  const { handle, eventSlug } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const timezone = search.timezone || Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  const selectedDate = parseDateOnly(search.date);
  const selectedSlot = search.slot ?? null;
  const selectedDuration = search.duration ?? null;
  const monthAnchor = parseMonth(search.month) ?? selectedDate ?? new Date();
  const slotRange = getSlotRange(monthAnchor);

  const { data: eventType, isLoading: eventTypeLoading } = api.useQuery("get", "/api/public/event-types/{handle}/{slug}", {
    params: { path: { handle, slug: eventSlug }, query: { privateLink: search.privateLink } }
  });
  const { data: slotsData, isLoading: slotsLoading } = api.useQuery(
    "get",
    "/api/public/slots",
    {
      params: {
        query: {
          Handle: handle,
          EventSlug: eventSlug,
          StartTime: slotRange.start.toISOString(),
          EndTime: slotRange.end.toISOString(),
          TimeZone: timezone,
          Duration: selectedDuration ?? eventType?.durationMinutes,
          PrivateLink: search.privateLink
        }
      }
    },
    { enabled: eventType !== undefined }
  );

  const updateSearch = (nextSearch: Partial<typeof search>) => {
    navigate({
      search: { ...search, ...nextSearch },
      replace: true
    });
  };

  return (
    <main className="min-h-screen bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8" data-testid="public-booker">
      <PublicBookerStore
        handle={handle}
        eventSlug={eventSlug}
        eventType={eventType ?? null}
        slotsByDate={slotsData?.slots ?? {}}
        isLoading={eventTypeLoading || slotsLoading}
        selectedDate={selectedDate}
        selectedSlot={selectedSlot}
        selectedDuration={selectedDuration}
        timezone={timezone}
        privateLink={search.privateLink}
        monthAnchor={monthAnchor}
        onDateChange={(date) => updateSearch({ date: formatDateOnly(date), slot: undefined, month: formatMonth(date) })}
        onMonthChange={(month) => updateSearch({ month: formatMonth(month), slot: undefined })}
        onSlotChange={(slot) =>
          updateSearch({ slot: slot.value, duration: selectedDuration ?? eventType?.durationMinutes })
        }
        onTimezoneChange={(nextTimezone) => updateSearch({ timezone: nextTimezone ?? undefined, slot: undefined })}
        onBackToTimes={() => updateSearch({ slot: undefined })}
      />
    </main>
  );
}

function PublicBookerStore({
  handle,
  eventSlug,
  eventType,
  slotsByDate,
  isLoading,
  selectedDate,
  selectedSlot,
  selectedDuration,
  timezone,
  privateLink,
  monthAnchor,
  onDateChange,
  onMonthChange,
  onSlotChange,
  onTimezoneChange,
  onBackToTimes
}: Readonly<{
  handle: string;
  eventSlug: string;
  eventType: PublicEventType | null;
  slotsByDate: Record<string, PublicSlot[]>;
  isLoading: boolean;
  selectedDate: Date | null;
  selectedSlot: string | null;
  selectedDuration: number | null;
  timezone: string;
  privateLink?: string;
  monthAnchor: Date;
  onDateChange: (date: Date) => void;
  onMonthChange: (date: Date) => void;
  onSlotChange: (slot: AvailableSlot) => void;
  onTimezoneChange: (timezone: string | null) => void;
  onBackToTimes: () => void;
}>) {
  const slots = useMemo(() => getAvailableSlots(slotsByDate, selectedDate), [selectedDate, slotsByDate]);
  const selectedSlotDate = useMemo(
    () => slots.find((slot) => slot.value === selectedSlot)?.startsAt ?? parseSlotValue(selectedSlot),
    [selectedSlot, slots]
  );
  const state: BookerState = isLoading
    ? "loading"
    : selectedSlot
      ? "booking"
      : selectedDate
        ? "selecting_time"
        : "selecting_date";

  return (
    <PublicBooker
      handle={handle}
      eventSlug={eventSlug}
      eventType={eventType}
      state={state}
      slotsByDate={slotsByDate}
      slots={slots}
      selectedDate={selectedDate}
      selectedSlot={selectedSlotDate}
      selectedDuration={selectedDuration}
      timezone={timezone}
      privateLink={privateLink}
      monthAnchor={monthAnchor}
      onDateChange={onDateChange}
      onMonthChange={onMonthChange}
      onSlotChange={onSlotChange}
      onTimezoneChange={onTimezoneChange}
      onBackToTimes={onBackToTimes}
    />
  );
}

function PublicBooker({
  handle,
  eventSlug,
  eventType,
  state,
  slotsByDate,
  slots,
  selectedDate,
  selectedSlot,
  selectedDuration,
  timezone,
  privateLink,
  monthAnchor,
  onDateChange,
  onMonthChange,
  onSlotChange,
  onTimezoneChange,
  onBackToTimes
}: Readonly<{
  handle: string;
  eventSlug: string;
  eventType: PublicEventType | null;
  state: BookerState;
  slotsByDate: Record<string, PublicSlot[]>;
  slots: AvailableSlot[];
  selectedDate: Date | null;
  selectedSlot: Date | null;
  selectedDuration: number | null;
  timezone: string;
  privateLink?: string;
  monthAnchor: Date;
  onDateChange: (date: Date) => void;
  onMonthChange: (date: Date) => void;
  onSlotChange: (slot: AvailableSlot) => void;
  onTimezoneChange: (timezone: string | null) => void;
  onBackToTimes: () => void;
}>) {
  if (state === "loading") return <PublicBookerSkeleton />;

  if (!eventType) {
    return (
      <div className="mx-auto flex min-h-[32rem] max-w-[70rem] items-center justify-center rounded-lg border bg-background p-8 text-center shadow-sm">
        <div className="flex max-w-[28rem] flex-col gap-2">
          <h1>
            <Trans>Booking page unavailable</Trans>
          </h1>
          <span className="text-sm text-muted-foreground">
            <Trans>This event type is not available for public booking yet.</Trans>
          </span>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto grid max-w-[70rem] overflow-hidden rounded-lg border bg-background shadow-sm lg:grid-cols-[20rem_1fr]">
      <EventMeta eventType={eventType} handle={handle} eventSlug={eventSlug} timezone={timezone} />
      <div className="grid min-h-[38rem] lg:grid-cols-[minmax(0,1fr)_18rem]">
        <section className="flex min-w-0 flex-col border-t p-4 sm:p-6 lg:border-t-0 lg:border-l">
          <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-col gap-1">
              <h2>
                <Trans>Select a date and time</Trans>
              </h2>
              <span className="text-sm text-muted-foreground">
                <Trans>Times are shown in your selected time zone.</Trans>
              </span>
            </div>
            <TimeZonePicker
              label={t`Time zone`}
              className="w-full sm:w-[18rem]"
              value={timezone}
              onValueChange={onTimezoneChange}
            />
          </div>
          <AvailableTimeSlots
            slotsByDate={slotsByDate}
            monthAnchor={monthAnchor}
            selectedDate={selectedDate}
            selectedSlot={selectedSlot}
            slots={slots}
            onDateChange={onDateChange}
            onMonthChange={onMonthChange}
            onSlotChange={onSlotChange}
          />
        </section>
        <aside className="hidden border-t bg-muted/20 p-4 lg:block lg:border-t-0 lg:border-l">
          <AvailableTimes
            selectedDate={selectedDate}
            selectedSlot={selectedSlot}
            slots={slots}
            onSlotChange={onSlotChange}
          />
        </aside>
      </div>
      {selectedSlot && (
        <BookEventForm
          handle={handle}
          eventSlug={eventSlug}
          eventType={eventType}
          selectedSlot={selectedSlot}
          selectedDuration={selectedDuration ?? eventType.durationMinutes}
          timezone={timezone}
          privateLink={privateLink}
          onBack={onBackToTimes}
        />
      )}
    </div>
  );
}

function EventMeta({
  eventType,
  handle,
  eventSlug,
  timezone
}: Readonly<{ eventType: PublicEventType; handle: string; eventSlug: string; timezone: string }>) {
  const location = eventType.locations?.[0] ?? { type: eventType.locationType, value: eventType.locationValue };

  return (
    <section className="flex flex-col gap-5 p-6" data-testid="public-booker-event-meta">
      <div className="flex items-center gap-3">
        <div className="flex size-12 items-center justify-center rounded-full bg-primary text-primary-foreground">
          <UserIcon className="size-5" />
        </div>
        <div className="min-w-0">
          <span className="block truncate text-sm font-medium">{eventType.profile?.displayName ?? `@${handle}`}</span>
          <span className="block truncate text-xs text-muted-foreground">/{eventSlug}</span>
        </div>
      </div>
      <div className="flex flex-col gap-2">
        <Badge variant="outline" className="w-fit">
          <Trans>Public booking</Trans>
        </Badge>
        <h1>{eventType.title}</h1>
        {eventType.description && (
          <span className="text-sm leading-6 text-muted-foreground">{eventType.description}</span>
        )}
      </div>
      <div className="flex flex-col gap-3 text-sm text-muted-foreground">
        <MetaRow icon={<ClockIcon />} text={formatMinutes(eventType.durationMinutes)} />
        <MetaRow icon={<CalendarIcon />} text={t`One-on-one`} />
        <MetaRow icon={<GlobeIcon />} text={timezone} />
        {location?.type && <MetaRow icon={<MapPinIcon />} text={formatLocation(location.type, location.value)} />}
      </div>
    </section>
  );
}

function AvailableTimeSlots({
  slotsByDate,
  monthAnchor,
  selectedDate,
  selectedSlot,
  slots,
  onDateChange,
  onMonthChange,
  onSlotChange
}: Readonly<{
  slotsByDate: Record<string, PublicSlot[]>;
  monthAnchor: Date;
  selectedDate: Date | null;
  selectedSlot: Date | null;
  slots: AvailableSlot[];
  onDateChange: (date: Date) => void;
  onMonthChange: (date: Date) => void;
  onSlotChange: (slot: AvailableSlot) => void;
}>) {
  const availableDates = useMemo(() => new Set(Object.keys(slotsByDate)), [slotsByDate]);
  const disabledDates = (date: Date) => !availableDates.has(formatDateOnly(date));

  return (
    <div className="grid gap-5 md:grid-cols-[minmax(0,1fr)_14rem] lg:grid-cols-1" data-testid="public-booker-slots">
      <div className="rounded-md border bg-background p-2">
        <Calendar
          mode="single"
          selected={selectedDate ?? undefined}
          defaultMonth={selectedDate ?? monthAnchor}
          disabledDates={disabledDates}
          onMonthChange={onMonthChange}
          onSelect={(date) => date && onDateChange(date)}
        />
      </div>
      <div className="lg:hidden">
        <AvailableTimes
          selectedDate={selectedDate}
          selectedSlot={selectedSlot}
          slots={slots}
          onSlotChange={onSlotChange}
        />
      </div>
    </div>
  );
}

function AvailableTimes({
  selectedDate,
  selectedSlot,
  slots,
  onSlotChange
}: Readonly<{
  selectedDate: Date | null;
  selectedSlot: Date | null;
  slots: AvailableSlot[];
  onSlotChange: (slot: AvailableSlot) => void;
}>) {
  return (
    <div className="flex flex-col gap-3" data-testid="public-booker-available-times">
      <div className="flex flex-col gap-1">
        <h3>
          <Trans>Available times</Trans>
        </h3>
        <span className="text-sm text-muted-foreground">
          {selectedDate ? formatLongDate(selectedDate) : t`Select a date to view times.`}
        </span>
      </div>
      {!selectedDate ? null : slots.length === 0 ? (
        <span className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          <Trans>No times available on this date.</Trans>
        </span>
      ) : (
        <div className="grid gap-2 sm:grid-cols-2 md:grid-cols-1">
          {slots.map((slot) => (
            <Button
              key={slot.value}
              type="button"
              variant={selectedSlot?.toISOString() === slot.value ? "default" : "outline"}
              className="w-full justify-center"
              data-testid="public-booker-time-slot"
              onClick={() => onSlotChange(slot)}
            >
              {slot.label}
            </Button>
          ))}
        </div>
      )}
    </div>
  );
}

function BookEventForm({
  handle,
  eventSlug,
  eventType,
  selectedSlot,
  selectedDuration,
  timezone,
  privateLink,
  onBack
}: Readonly<{
  handle: string;
  eventSlug: string;
  eventType: PublicEventType;
  selectedSlot: Date;
  selectedDuration: number;
  timezone: string;
  privateLink?: string;
  onBack: () => void;
}>) {
  const mutation = api.useMutation("post", "/api/public/bookings");
  const isConfirmed = mutation.isSuccess;

  return (
    <div
      className="fixed inset-0 z-40 flex bg-background lg:static lg:col-span-2 lg:border-t"
      data-testid="public-booker-form"
    >
      <div className="mx-auto flex w-full max-w-[42rem] flex-col gap-5 overflow-auto p-5 sm:p-6 lg:max-w-none lg:p-6">
        <div className="flex items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h2>{isConfirmed ? <Trans>Booking confirmed</Trans> : <Trans>Enter your details</Trans>}</h2>
            <span className="text-sm text-muted-foreground">
              {`${formatLongDate(selectedSlot)} · ${formatTime(selectedSlot)} · ${formatMinutes(selectedDuration)}`}
            </span>
          </div>
          <Button type="button" variant="ghost" size="icon-sm" aria-label={t`Back to times`} onClick={onBack}>
            <XIcon />
          </Button>
        </div>
        {isConfirmed ? (
          <div className="flex flex-col items-start gap-3 rounded-md border bg-muted/20 p-4">
            <div className="flex size-10 items-center justify-center rounded-full bg-primary text-primary-foreground">
              <CheckIcon className="size-5" />
            </div>
            <span className="font-medium">
              <Trans>Your booking is confirmed.</Trans>
            </span>
            <span className="text-sm text-muted-foreground">
              <Trans>A confirmation will be sent to the email address you provided.</Trans>
            </span>
          </div>
        ) : (
          <BookEventFormFields
            eventType={eventType}
            error={mutation.error}
            isPending={mutation.isPending}
            onSubmit={(values) =>
              mutation.mutate({
                body: {
                  handle,
                  eventSlug,
                  startTime: selectedSlot.toISOString(),
                  duration: selectedDuration,
                  timeZone: timezone,
                  bookerName: values.bookerName,
                  bookerEmail: values.bookerEmail,
                  responses: values.responses,
                  privateLink: privateLink ?? null
                }
              })
            }
          />
        )}
      </div>
    </div>
  );
}

function BookEventFormFields({
  eventType,
  error,
  isPending,
  onSubmit
}: Readonly<{
  eventType: PublicEventType;
  error: Parameters<typeof GeneralApiErrors>[0]["error"];
  isPending: boolean;
  onSubmit: (values: { bookerName: string; bookerEmail: string; responses: Record<string, string> }) => void;
}>) {
  return (
    <Form
      className="gap-5"
      validationErrors={error?.errors}
      onSubmit={(event) => {
        event.preventDefault();
        const formData = new FormData(event.currentTarget);
        const responses = Object.fromEntries(
          (eventType.bookingFields ?? []).map((field) => [field.name, String(formData.get(field.name) ?? "")])
        );
        const notes = String(formData.get("notes") ?? "").trim();
        if (notes) responses.notes = notes;
        onSubmit({
          bookerName: String(formData.get("name") ?? ""),
          bookerEmail: String(formData.get("email") ?? ""),
          responses
        });
      }}
    >
      <GeneralApiErrors error={error} />
      <BookingFields eventType={eventType} />
      <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button type="submit" data-testid="public-booker-submit" isPending={isPending}>
          <Trans>Confirm booking</Trans>
        </Button>
      </div>
    </Form>
  );
}

function BookingFields({ eventType }: Readonly<{ eventType: PublicEventType }>) {
  return (
    <div className="grid gap-4 sm:grid-cols-2" data-testid="public-booker-booking-fields">
      <TextField name="name" label={t`Name`} autoComplete="name" required />
      <TextField name="email" label={t`Email`} type="email" autoComplete="email" required />
      {(eventType.bookingFields ?? []).map((field) =>
        field.type === "textarea" ? (
          <TextAreaField
            key={field.name}
            name={field.name}
            label={field.label}
            required={field.required}
            className="sm:col-span-2"
          />
        ) : (
          <TextField key={field.name} name={field.name} label={field.label} required={field.required} />
        )
      )}
      <TextAreaField name="notes" label={t`Additional notes`} className="sm:col-span-2" />
    </div>
  );
}

function MetaRow({ icon, text }: Readonly<{ icon: React.ReactNode; text: string }>) {
  return (
    <span className="flex items-center gap-2">
      <span className="[&_svg]:size-4">{icon}</span>
      <span>{text}</span>
    </span>
  );
}

function PublicBookerSkeleton() {
  return (
    <div className="mx-auto grid max-w-[70rem] gap-0 overflow-hidden rounded-lg border bg-background shadow-sm lg:grid-cols-[20rem_1fr]">
      <div className="flex flex-col gap-5 p-6">
        <Skeleton className="size-12 rounded-full" />
        <Skeleton className="h-8 w-3/4" />
        <Skeleton className="h-20 w-full" />
      </div>
      <div className="grid min-h-[38rem] border-t p-6 lg:border-t-0 lg:border-l">
        <Skeleton className="h-full min-h-[28rem] w-full" />
      </div>
    </div>
  );
}

function getAvailableSlots(slotsByDate: Record<string, PublicSlot[]>, selectedDate: Date | null): AvailableSlot[] {
  if (!selectedDate) return [];
  return (slotsByDate[formatDateOnly(selectedDate)] ?? []).map((slot) => {
    const startsAt = new Date(slot.time);
    return { ...slot, startsAt, label: formatTime(startsAt), value: startsAt.toISOString() };
  });
}

function getSlotRange(month: Date) {
  const start = new Date(Date.UTC(month.getFullYear(), month.getMonth(), 1, 0, 0, 0, 0));
  const end = new Date(Date.UTC(month.getFullYear(), month.getMonth() + 1, 1, 0, 0, 0, 0));
  return { start, end };
}

function formatLocation(type: string, value: string | null | undefined) {
  if (type === "link") return value || t`Video call`;
  if (type === "phone") return t`Phone call`;
  if (type === "inPerson" || type === "in-person") return value || t`In person`;
  return value || type;
}

function stringValue(value: unknown) {
  return typeof value === "string" && value.trim() ? value : undefined;
}

function numberValue(value: unknown) {
  const number = typeof value === "string" ? Number(value) : undefined;
  return number && Number.isFinite(number) ? number : undefined;
}

function parseDateOnly(value: string | undefined) {
  if (!value) return null;
  const date = new Date(`${value}T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function parseMonth(value: string | undefined) {
  if (!value) return null;
  const date = new Date(`${value}-01T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}

function parseSlotValue(value: string | null) {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

function formatDateOnly(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function formatMonth(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
}

function formatLongDate(date: Date) {
  return new Intl.DateTimeFormat(undefined, { weekday: "long", month: "long", day: "numeric" }).format(date);
}

function formatTime(date: Date) {
  return new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).format(date);
}

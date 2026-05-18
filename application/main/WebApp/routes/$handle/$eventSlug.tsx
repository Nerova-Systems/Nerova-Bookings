import { createFileRoute, useNavigate } from "@tanstack/react-router";

import { api } from "@/shared/lib/api/client";

import { PublicBooker } from "./-booker/PublicBooker";
import {
  formatDateOnly,
  formatMonth,
  getSlotRange,
  numberValue,
  parseDateOnly,
  parseMonth,
  stringValue
} from "./-booker/publicBookerTypes";

export const Route = createFileRoute("/$handle/$eventSlug")({
  staticData: { trackingTitle: "Public booker" },
  validateSearch: (search: Record<string, unknown>) => ({
    month: stringValue(search.month),
    date: stringValue(search.date),
    slot: stringValue(search.slot),
    duration: numberValue(search.duration),
    timezone: stringValue(search.timezone) ?? stringValue(search["cal.tz"]),
    privateLink: stringValue(search.privateLink),
    rescheduleUid: stringValue(search.rescheduleUid),
    rescheduledBy: stringValue(search.rescheduledBy)
  }),
  component: PublicBookerWebWrapper
});

function PublicBookerWebWrapper() {
  const { handle, eventSlug } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const timezone = search.timezone || Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  const selectedDate = parseDateOnly(search.date);
  const selectedDuration = search.duration ?? null;
  const monthAnchor = parseMonth(search.month) ?? selectedDate ?? new Date();
  const slotRange = getSlotRange(monthAnchor);

  const { data: eventType, isLoading: eventTypeLoading } = api.useQuery(
    "get",
    "/api/public/event-types/{handle}/{slug}",
    {
      params: { path: { handle, slug: eventSlug }, query: { privateLink: search.privateLink } }
    }
  );
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
  const {
    data: rescheduleBooking,
    error: rescheduleError,
    isLoading: rescheduleLoading
  } = api.useQuery(
    "get",
    "/api/public/reschedule-bookings/{id}",
    {
      params: {
        path: { id: search.rescheduleUid ?? "" },
        query: { handle, eventSlug }
      }
    },
    { enabled: search.rescheduleUid !== undefined }
  );

  const updateSearch = (nextSearch: Partial<typeof search>) => {
    navigate({ search: { ...search, ...nextSearch }, replace: true });
  };

  return (
    <main className="min-h-screen bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8" data-testid="public-booker">
      <PublicBooker
        handle={handle}
        eventSlug={eventSlug}
        eventType={eventType ?? null}
        rescheduleBooking={rescheduleBooking ?? null}
        rescheduleUnavailable={
          search.rescheduleUid !== undefined &&
          ((rescheduleError !== undefined && rescheduleError !== null) || rescheduleBooking?.canReschedule === false)
        }
        slotsByDate={slotsData?.slots ?? {}}
        isLoading={eventTypeLoading || slotsLoading || rescheduleLoading}
        selectedDate={selectedDate}
        selectedSlot={search.slot ?? null}
        selectedDuration={selectedDuration}
        timezone={timezone}
        privateLink={search.privateLink}
        rescheduledBy={search.rescheduledBy}
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

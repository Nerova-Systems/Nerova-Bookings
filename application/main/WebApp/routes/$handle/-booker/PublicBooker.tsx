import { Trans } from "@lingui/react/macro";

import { AvailableTimeSlots } from "./PublicBookerAvailability";
import { BookEventForm } from "./PublicBookerForm";
import { EventMeta } from "./PublicBookerMeta";
import { PublicBookerSkeleton } from "./PublicBookerSkeleton";
import {
  type AvailableSlot,
  type BookerState,
  getAvailableSlots,
  parseSlotValue,
  type PublicEventType,
  type PublicRescheduleBooking,
  type PublicSlot
} from "./publicBookerTypes";

export function PublicBooker({
  handle,
  eventSlug,
  eventType,
  rescheduleBooking,
  rescheduleUnavailable,
  slotsByDate,
  isLoading,
  selectedDate,
  selectedSlot,
  selectedDuration,
  timezone,
  privateLink,
  rescheduledBy,
  monthAnchor,
  onDateChange,
  onMonthChange,
  onSlotChange,
  onTimezoneChange,
  onBookingComplete,
  onBackToTimes
}: Readonly<{
  handle: string;
  eventSlug: string;
  eventType: PublicEventType | null;
  rescheduleBooking: PublicRescheduleBooking | null;
  rescheduleUnavailable: boolean;
  slotsByDate: Record<string, PublicSlot[]>;
  isLoading: boolean;
  selectedDate: Date | null;
  selectedSlot: string | null;
  selectedDuration: number | null;
  timezone: string;
  privateLink?: string;
  rescheduledBy?: string;
  monthAnchor: Date;
  onDateChange: (date: Date) => void;
  onMonthChange: (date: Date) => void;
  onSlotChange: (slot: AvailableSlot) => void;
  onTimezoneChange: (timezone: string | null) => void;
  onBookingComplete: () => void;
  onBackToTimes: () => void;
}>) {
  const slots = getAvailableSlots(slotsByDate, selectedDate);
  const selectedSlotDate = slots.find((slot) => slot.value === selectedSlot)?.startsAt ?? parseSlotValue(selectedSlot);
  const state: BookerState = isLoading
    ? "loading"
    : selectedSlot
      ? "booking"
      : selectedDate
        ? "selecting_time"
        : "selecting_date";

  if (state === "loading") return <PublicBookerSkeleton />;
  if (!eventType || rescheduleUnavailable) return <PublicBookerUnavailable />;

  return (
    <div
      data-testid="booker-container"
      data-booker-state={state}
      className="mx-auto grid w-full max-w-[70rem] items-start overflow-hidden rounded-md border border-border bg-background shadow-sm transition-[width] duration-300 lg:grid-cols-[20rem_minmax(0,1fr)]"
    >
      <EventMeta eventType={eventType} handle={handle} eventSlug={eventSlug} timezone={timezone} />
      {!selectedSlotDate ? (
        <div className="grid min-h-[38rem] min-w-0 lg:grid-cols-[minmax(0,1fr)_18rem]">
          <AvailableTimeSlots
            slotsByDate={slotsByDate}
            monthAnchor={monthAnchor}
            selectedDate={selectedDate}
            selectedSlot={selectedSlotDate}
            slots={slots}
            timezone={timezone}
            onDateChange={onDateChange}
            onMonthChange={onMonthChange}
            onSlotChange={onSlotChange}
            onTimezoneChange={onTimezoneChange}
          />
        </div>
      ) : (
        <BookEventForm
          handle={handle}
          eventSlug={eventSlug}
          eventType={eventType}
          rescheduleBooking={rescheduleBooking}
          selectedSlot={selectedSlotDate}
          selectedDuration={selectedDuration ?? eventType.durationMinutes}
          timezone={timezone}
          privateLink={privateLink}
          rescheduledBy={rescheduledBy}
          onBookingComplete={onBookingComplete}
          onBack={onBackToTimes}
        />
      )}
    </div>
  );
}

function PublicBookerUnavailable() {
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

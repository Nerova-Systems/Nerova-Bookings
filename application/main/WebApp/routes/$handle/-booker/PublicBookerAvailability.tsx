import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Calendar } from "@repo/ui/components/Calendar";
import { TimeZonePicker } from "@repo/ui/components/TimeZonePicker";
import { useMemo } from "react";

import { type AvailableSlot, formatDateOnly, formatLongDate, type PublicSlot } from "./publicBookerTypes";

export function AvailableTimeSlots({
  slotsByDate,
  monthAnchor,
  selectedDate,
  selectedSlot,
  slots,
  timezone,
  onDateChange,
  onMonthChange,
  onSlotChange,
  onTimezoneChange
}: Readonly<{
  slotsByDate: Record<string, PublicSlot[]>;
  monthAnchor: Date;
  selectedDate: Date | null;
  selectedSlot: Date | null;
  slots: AvailableSlot[];
  timezone: string;
  onDateChange: (date: Date) => void;
  onMonthChange: (date: Date) => void;
  onSlotChange: (slot: AvailableSlot) => void;
  onTimezoneChange: (timezone: string | null) => void;
}>) {
  const availableDates = useMemo(() => new Set(Object.keys(slotsByDate)), [slotsByDate]);
  const disabledDates = (date: Date) => !availableDates.has(formatDateOnly(date));

  return (
    <>
      <section
        className="flex min-w-0 flex-col border-t p-4 sm:p-6 lg:border-t-0 lg:border-l"
        data-testid="booker-date-picker"
      >
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
          <div className="lg:hidden" data-testid="booker-timeslots-mobile">
            <AvailableTimes
              selectedDate={selectedDate}
              selectedSlot={selectedSlot}
              slots={slots}
              onSlotChange={onSlotChange}
            />
          </div>
        </div>
      </section>
      <aside
        className="hidden border-t bg-muted/20 p-4 lg:block lg:border-t-0 lg:border-l"
        data-testid="booker-timeslots"
      >
        <AvailableTimes
          selectedDate={selectedDate}
          selectedSlot={selectedSlot}
          slots={slots}
          onSlotChange={onSlotChange}
        />
      </aside>
    </>
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

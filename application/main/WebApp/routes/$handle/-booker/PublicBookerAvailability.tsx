import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Calendar } from "@repo/ui/components/Calendar";
import { TimeZonePicker } from "@repo/ui/components/TimeZonePicker";
import { XIcon } from "lucide-react";
import { useMemo } from "react";

import { type AvailableSlot, formatDateOnly, formatLongDate, type PublicSlot } from "./publicBookerTypes";

type PublicBookerAvailabilityProps = Readonly<{
  slotsByDate: Record<string, PublicSlot[]>;
  monthAnchor: Date;
  selectedDate: Date | null;
  selectedSlot: Date | null;
  slots: AvailableSlot[];
  timezone: string;
  eventTitle?: string;
  onDateChange: (date: Date) => void;
  onMonthChange: (date: Date) => void;
  onSlotChange: (slot: AvailableSlot) => void;
  onTimezoneChange: (timezone: string | null) => void;
  onCloseSlots?: () => void;
}>;

export function AvailableTimeSlots({
  slotsByDate,
  monthAnchor,
  selectedDate,
  selectedSlot,
  slots,
  timezone,
  eventTitle,
  onDateChange,
  onMonthChange,
  onSlotChange,
  onTimezoneChange,
  onCloseSlots
}: PublicBookerAvailabilityProps) {
  return (
    <>
      <DatePickerSection
        slotsByDate={slotsByDate}
        monthAnchor={monthAnchor}
        selectedDate={selectedDate}
        timezone={timezone}
        onDateChange={onDateChange}
        onMonthChange={onMonthChange}
        onTimezoneChange={onTimezoneChange}
      />
      <TimeSlotsSection
        eventTitle={eventTitle}
        selectedDate={selectedDate}
        selectedSlot={selectedSlot}
        slots={slots}
        onSlotChange={onSlotChange}
        onClose={onCloseSlots}
      />
    </>
  );
}

export function DatePickerSection({
  slotsByDate,
  monthAnchor,
  selectedDate,
  timezone,
  onDateChange,
  onMonthChange,
  onTimezoneChange
}: Pick<
  PublicBookerAvailabilityProps,
  | "slotsByDate"
  | "monthAnchor"
  | "selectedDate"
  | "timezone"
  | "onDateChange"
  | "onMonthChange"
  | "onTimezoneChange"
>) {
  const availableDates = useMemo(() => new Set(Object.keys(slotsByDate)), [slotsByDate]);
  const disabledDates = (date: Date) => !availableDates.has(formatDateOnly(date));

  return (
    <section
      className="flex min-w-0 flex-col border-t p-5 md:w-(--booker-main-width) md:border-t-0 md:border-l md:px-5 md:py-3"
      data-testid="booker-date-picker"
    >
      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between md:hidden">
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
      <div className="flex justify-center" data-testid="public-booker-slots">
        <Calendar
          mode="single"
          selected={selectedDate ?? undefined}
          defaultMonth={selectedDate ?? monthAnchor}
          disabledDates={disabledDates}
          onMonthChange={onMonthChange}
          onSelect={(date) => date && onDateChange(date)}
        />
      </div>
    </section>
  );
}

export function TimeSlotsSection({
  eventTitle,
  selectedDate,
  selectedSlot,
  slots,
  onSlotChange,
  onClose
}: Pick<PublicBookerAvailabilityProps, "eventTitle" | "selectedDate" | "selectedSlot" | "slots" | "onSlotChange"> & {
  onClose?: () => void;
}) {
  return (
    <aside
      className="fixed inset-0 z-50 overflow-auto bg-background p-8 md:static md:z-auto md:w-(--booker-timeslots-width) md:overflow-visible md:border-l md:px-5 md:py-3"
      data-testid="booker-timeslots"
    >
      <div className="mb-6 flex items-start justify-between gap-4 md:hidden">
        <div className="flex min-w-0 flex-col gap-1">
          <span className="truncate font-medium">{eventTitle ?? t`Available times`}</span>
          <span className="text-sm text-muted-foreground">
            {selectedDate ? formatLongDate(selectedDate) : t`Select a date to view times.`}
          </span>
        </div>
        <Button type="button" variant="ghost" size="icon-sm" aria-label={t`Back to dates`} onClick={onClose}>
          <XIcon />
        </Button>
      </div>
      <AvailableTimes selectedDate={selectedDate} selectedSlot={selectedSlot} slots={slots} onSlotChange={onSlotChange} />
    </aside>
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

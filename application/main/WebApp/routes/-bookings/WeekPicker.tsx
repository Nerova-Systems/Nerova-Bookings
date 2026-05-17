import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import { Calendar } from "@repo/ui/components/Calendar";
import { Popover, PopoverContent, PopoverTrigger } from "@repo/ui/components/Popover";
import { CalendarIcon } from "lucide-react";
import { useState } from "react";

import { getWeekStartDate, toDateInputValue } from "./bookingTypes";

export function WeekPicker({
  weekStart,
  onWeekStartChange
}: Readonly<{
  weekStart: Date;
  onWeekStartChange: (weekStart: Date) => void;
}>) {
  const [isOpen, setIsOpen] = useState(false);
  const weekEnd = new Date(weekStart);
  weekEnd.setDate(weekEnd.getDate() + 6);
  const weekRange = formatWeekRange(weekStart, weekEnd);

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger
        render={
          <Button type="button" variant="secondary" size="sm" aria-label={t`Select week`}>
            <CalendarIcon />
            {weekRange}
          </Button>
        }
      />
      <PopoverContent className="w-auto p-0" align="start">
        <Calendar
          mode="single"
          selected={weekStart}
          defaultMonth={weekStart}
          onSelect={(date) => {
            if (!date) return;
            onWeekStartChange(getWeekStartDate(date));
            setIsOpen(false);
          }}
        />
      </PopoverContent>
    </Popover>
  );
}

export function parseWeekStart(value: string | undefined) {
  if (!value) return getWeekStartDate(new Date());

  const parsedDate = new Date(`${value}T00:00:00`);
  return Number.isNaN(parsedDate.getTime()) ? getWeekStartDate(new Date()) : getWeekStartDate(parsedDate);
}

export function formatWeekStartSearchValue(weekStart: Date) {
  return toDateInputValue(getWeekStartDate(weekStart));
}

function formatWeekRange(weekStart: Date, weekEnd: Date) {
  const startFormatter = new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric" });
  const endFormatter = new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", year: "numeric" });
  return `${startFormatter.format(weekStart)} - ${endFormatter.format(weekEnd)}`;
}

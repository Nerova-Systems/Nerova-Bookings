import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { CopyIcon, PlusIcon, Trash2Icon } from "lucide-react";

import { formatMinutes, parseTime } from "./schedulingTypes";

type DailyRange = { startMinute: number; endMinute: number };

export function DayRanges({
  day,
  ranges,
  updateDay,
  copyRangesToActiveDays
}: Readonly<{
  day: number;
  ranges: DailyRange[];
  updateDay: (day: number, ranges: DailyRange[]) => void;
  copyRangesToActiveDays: (ranges: DailyRange[]) => void;
}>) {
  return (
    <div className="flex min-w-0 flex-col gap-2">
      {ranges.map((range, index) => (
        <div key={index} className="flex flex-wrap items-center gap-2">
          <Input
            aria-label={t`Start`}
            className="h-9 w-28"
            value={formatMinutes(range.startMinute)}
            onChange={(event) =>
              updateDay(
                day,
                ranges.map((currentRange, currentIndex) =>
                  currentIndex === index
                    ? { ...currentRange, startMinute: parseTime(event.target.value, currentRange.startMinute) }
                    : currentRange
                )
              )
            }
          />
          <span className="text-muted-foreground">-</span>
          <Input
            aria-label={t`End`}
            className="h-9 w-28"
            value={formatMinutes(range.endMinute)}
            onChange={(event) =>
              updateDay(
                day,
                ranges.map((currentRange, currentIndex) =>
                  currentIndex === index
                    ? { ...currentRange, endMinute: parseTime(event.target.value, currentRange.endMinute) }
                    : currentRange
                )
              )
            }
          />
          {index === 0 && (
            <WindowActions
              day={day}
              ranges={ranges}
              updateDay={updateDay}
              copyRangesToActiveDays={copyRangesToActiveDays}
            />
          )}
          {ranges.length > 1 && (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label={t`Remove time`}
              onClick={() =>
                updateDay(
                  day,
                  ranges.filter((_, currentIndex) => currentIndex !== index)
                )
              }
            >
              <Trash2Icon />
            </Button>
          )}
        </div>
      ))}
    </div>
  );
}

function WindowActions({
  day,
  ranges,
  updateDay,
  copyRangesToActiveDays
}: Readonly<{
  day: number;
  ranges: DailyRange[];
  updateDay: (day: number, ranges: DailyRange[]) => void;
  copyRangesToActiveDays: (ranges: DailyRange[]) => void;
}>) {
  return (
    <>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label={t`Add time`}
        onClick={() => updateDay(day, [...ranges, { startMinute: 1020, endMinute: 1080 }])}
      >
        <PlusIcon />
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label={t`Copy times`}
        onClick={() => copyRangesToActiveDays(ranges)}
      >
        <CopyIcon />
      </Button>
    </>
  );
}

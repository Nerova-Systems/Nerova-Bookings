import { t } from "@lingui/core/macro";
import { Switch } from "@repo/ui/components/Switch";

import type { AvailabilityWindow } from "./schedulingTypes";

import { DayRanges } from "./AvailabilityWindowRanges";
import { getOverlappingAvailabilityWindowIndexes } from "./schedulingTypes";

const DEFAULT_WINDOW = { startMinute: 540, endMinute: 1020 };

type DailyRange = { startMinute: number; endMinute: number };

function getWeekDays() {
  return [
    { value: 0, label: t`Sunday` },
    { value: 1, label: t`Monday` },
    { value: 2, label: t`Tuesday` },
    { value: 3, label: t`Wednesday` },
    { value: 4, label: t`Thursday` },
    { value: 5, label: t`Friday` },
    { value: 6, label: t`Saturday` }
  ];
}

function toDailyWindows(windows: AvailabilityWindow[]) {
  const dailyWindows: DailyRange[][] = [[], [], [], [], [], [], []];
  windows.forEach((window) => {
    window.days.forEach((day) => {
      if (day >= 0 && day <= 6)
        dailyWindows[day].push({ startMinute: window.startMinute, endMinute: window.endMinute });
    });
  });

  return dailyWindows;
}

function fromDailyWindows(dailyWindows: DailyRange[][]): AvailabilityWindow[] {
  return dailyWindows.flatMap((ranges, day) =>
    ranges.map((range) => ({ days: [day], startMinute: range.startMinute, endMinute: range.endMinute }))
  );
}

function getDayErrors({
  ranges,
  dayWindowIndexes,
  overlappingIndexes
}: Readonly<{ ranges: DailyRange[]; dayWindowIndexes: number[]; overlappingIndexes: Set<number> }>) {
  return [
    ...ranges.flatMap((range) => [range.startMinute >= range.endMinute ? t`End time must be after start time.` : null]),
    dayWindowIndexes.some((index) => overlappingIndexes.has(index))
      ? t`This window overlaps another window on the same day.`
      : null
  ].filter((message): message is string => Boolean(message));
}

export function WindowEditor({
  windows,
  onChange
}: Readonly<{ windows: AvailabilityWindow[]; onChange: (windows: AvailabilityWindow[]) => void }>) {
  const weekDays = getWeekDays();
  const dailyWindows = toDailyWindows(windows);
  const overlappingIndexes = getOverlappingAvailabilityWindowIndexes(windows);

  const updateDay = (day: number, ranges: DailyRange[]) => {
    const nextDailyWindows = dailyWindows.map((currentRanges, currentDay) =>
      currentDay === day ? ranges : currentRanges
    );
    onChange(fromDailyWindows(nextDailyWindows));
  };

  const copyRangesToActiveDays = (ranges: DailyRange[]) => {
    const sourceRanges = ranges.map((currentRange) => ({ ...currentRange }));
    onChange(
      fromDailyWindows(
        dailyWindows.map((currentRanges) =>
          currentRanges.length > 0 ? sourceRanges.map((sourceRange) => ({ ...sourceRange })) : currentRanges
        )
      )
    );
  };

  return (
    <div className="rounded-md border p-5">
      <div className="flex flex-col gap-4">
        {weekDays.map((day) => {
          const ranges = dailyWindows[day.value];
          const dayWindowIndexes = windows
            .map((window, index) => (window.days.includes(day.value) ? index : -1))
            .filter((index) => index >= 0);
          const errors = getDayErrors({ ranges, dayWindowIndexes, overlappingIndexes });

          return (
            <div key={day.value} className="flex flex-col gap-2">
              <div className="grid items-start gap-3 md:grid-cols-[9rem_minmax(0,1fr)]">
                <label className="flex h-9 items-center gap-3">
                  <Switch
                    checked={ranges.length > 0}
                    onCheckedChange={(checked) => updateDay(day.value, checked ? [{ ...DEFAULT_WINDOW }] : [])}
                  />
                  <span className="text-sm font-medium">{day.label}</span>
                </label>
                {ranges.length > 0 && (
                  <DayRanges
                    day={day.value}
                    ranges={ranges}
                    updateDay={updateDay}
                    copyRangesToActiveDays={copyRangesToActiveDays}
                  />
                )}
              </div>
              {errors.length > 0 && (
                <div className="ml-0 flex flex-col gap-1 text-sm text-destructive md:ml-48">
                  {errors.map((message) => (
                    <div key={message}>{message}</div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

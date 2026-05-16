import type { Schemas } from "@/shared/lib/api/client";

export type Schedule = Schemas["ScheduleResponse"];
export type SchedulePayload = Schemas["CreateScheduleCommand"];
export type AvailabilityWindow = Schemas["AvailabilityWindowRequest"];
export type EventType = Schemas["EventTypeResponse"];
export type EventTypePayload = Schemas["CreateEventTypeCommand"];
export type ApiValidationError = Schemas["HttpValidationProblemDetails"] | null | undefined;

export function newSchedulePayload(isDefault = false): SchedulePayload {
  return {
    name: isDefault ? "Default schedule" : "Working hours",
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
    isDefault,
    availabilityWindows: [{ days: [1, 2, 3, 4, 5], startMinute: 540, endMinute: 1020 }]
  };
}

export function scheduleToPayload(schedule: Schedule): SchedulePayload {
  return {
    name: schedule.name,
    timeZone: schedule.timeZone,
    isDefault: schedule.isDefault,
    availabilityWindows: schedule.availabilityWindows.map((window) => ({
      days: [...window.days],
      startMinute: window.startMinute,
      endMinute: window.endMinute
    }))
  };
}

export function newEventTypePayload(scheduleId: string): EventTypePayload {
  return {
    title: "",
    slug: "",
    description: null,
    durationMinutes: 30,
    hidden: false,
    scheduleId,
    beforeEventBufferMinutes: 0,
    afterEventBufferMinutes: 0,
    slotIntervalMinutes: 30,
    minimumBookingNoticeMinutes: 60,
    locationType: "link",
    locationValue: ""
  };
}

export function isSchedulePayloadSubmittable(value: SchedulePayload) {
  return (
    value.name.trim().length > 0 &&
    value.timeZone.trim().length > 0 &&
    value.availabilityWindows.length > 0 &&
    getOverlappingAvailabilityWindowIndexes(value.availabilityWindows).size === 0 &&
    value.availabilityWindows.every(
      (window) =>
        window.days.length > 0 &&
        window.days.every((day) => day >= 0 && day <= 6) &&
        window.startMinute >= 0 &&
        window.endMinute <= 1440 &&
        window.startMinute < window.endMinute
    )
  );
}

export function isEventTypePayloadSubmittable(value: EventTypePayload) {
  return (
    value.title.trim().length > 0 &&
    value.slug.trim().length > 0 &&
    value.scheduleId.trim().length > 0 &&
    value.durationMinutes >= 5 &&
    value.slotIntervalMinutes >= 5 &&
    value.minimumBookingNoticeMinutes >= 0 &&
    value.beforeEventBufferMinutes >= 0 &&
    value.afterEventBufferMinutes >= 0
  );
}

export function eventTypeToPayload(eventType: EventType): EventTypePayload {
  return {
    title: eventType.title,
    slug: eventType.slug,
    description: eventType.description,
    durationMinutes: eventType.durationMinutes,
    hidden: eventType.hidden,
    scheduleId: eventType.scheduleId,
    beforeEventBufferMinutes: eventType.beforeEventBufferMinutes,
    afterEventBufferMinutes: eventType.afterEventBufferMinutes,
    slotIntervalMinutes: eventType.slotIntervalMinutes,
    minimumBookingNoticeMinutes: eventType.minimumBookingNoticeMinutes,
    locationType: eventType.locationType,
    locationValue: eventType.locationValue
  };
}

export function slugify(value: string) {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}

export function formatMinutes(totalMinutes: number) {
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
}

export function formatAvailabilityWindows(
  windows: AvailabilityWindow[],
  weekdayLabels: Record<number, string>,
  emptyLabel: string
) {
  if (windows.length === 0) return emptyLabel;

  return windows
    .map((window) => {
      const days = formatWeekdayRanges(window.days, weekdayLabels);
      return `${days}: ${formatMinutes(window.startMinute)}-${formatMinutes(window.endMinute)}`;
    })
    .join("; ");
}

function formatWeekdayRanges(days: number[], weekdayLabels: Record<number, string>) {
  const sortedDays = [...new Set(days)].sort((left, right) => left - right);
  const ranges: string[] = [];

  for (let index = 0; index < sortedDays.length; index++) {
    const rangeStart = sortedDays[index];
    let rangeEnd = rangeStart;

    while (sortedDays[index + 1] === rangeEnd + 1) {
      rangeEnd = sortedDays[index + 1];
      index++;
    }

    ranges.push(
      rangeStart === rangeEnd ? weekdayLabels[rangeStart] : `${weekdayLabels[rangeStart]}-${weekdayLabels[rangeEnd]}`
    );
  }

  return ranges.join(", ");
}

export function getOverlappingAvailabilityWindowIndexes(windows: AvailabilityWindow[]) {
  const overlappingIndexes = new Set<number>();

  windows.forEach((window, index) => {
    window.days.forEach((day) => {
      windows.forEach((comparisonWindow, comparisonIndex) => {
        if (comparisonIndex <= index || !comparisonWindow.days.includes(day)) return;

        const overlaps =
          window.startMinute < comparisonWindow.endMinute && comparisonWindow.startMinute < window.endMinute;
        if (overlaps) {
          overlappingIndexes.add(index);
          overlappingIndexes.add(comparisonIndex);
        }
      });
    });
  });

  return overlappingIndexes;
}

export function parseTime(value: string, fallback: number) {
  const match = /^(\d{1,2}):(\d{2})$/.exec(value.trim());
  if (!match) return fallback;

  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (hours < 0 || hours > 24 || minutes < 0 || minutes > 59) return fallback;

  return Math.min(hours * 60 + minutes, 1440);
}

export function getApiErrorMessages(error: ApiValidationError) {
  return [error?.detail, ...Object.values(error?.errors ?? {}).flat()].filter(Boolean);
}

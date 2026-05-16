import type { Schemas } from "@/shared/lib/api/client";

export type Schedule = Schemas["ScheduleResponse"];
export type SchedulePayload = Schemas["CreateScheduleCommand"];
export type AvailabilityWindow = Schemas["AvailabilityWindowRequest"];
export type EventType = Schemas["EventTypeResponse"];
export type EventTypePayload = Schemas["CreateEventTypeCommand"];
export type ApiValidationError = Schemas["HttpValidationProblemDetails"] | null | undefined;

export function newSchedulePayload(): SchedulePayload {
  return {
    name: "",
    timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC",
    isDefault: false,
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

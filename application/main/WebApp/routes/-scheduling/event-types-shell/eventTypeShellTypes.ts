import { t } from "@lingui/core/macro";

import type { EventType, EventTypePayload, Schedule } from "../schedulingTypes";

export type EventTypeTabName = "setup" | "availability" | "limits" | "advanced";

export const eventTypeTabs: { name: EventTypeTabName; label: string }[] = [
  { name: "setup", label: t`Setup` },
  { name: "availability", label: t`Availability` },
  { name: "limits", label: t`Limits` },
  { name: "advanced", label: t`Advanced` }
];

export function isEventTypeTabName(value: unknown): value is EventTypeTabName {
  return value === "setup" || value === "availability" || value === "limits" || value === "advanced";
}

export function getScheduleName(scheduleId: string, schedules: Schedule[]) {
  return schedules.find((schedule) => schedule.id === scheduleId)?.name ?? t`Schedule unavailable`;
}

export function getEventTypePublicUrl(eventType: Pick<EventType, "slug">) {
  return `/book/${eventType.slug}`;
}

export function eventTypeToDuplicatePayload(eventType: EventType): EventTypePayload {
  return {
    title: t`Copy of ${eventType.title}`,
    slug: `${eventType.slug}-copy`,
    description: eventType.description,
    durationMinutes: eventType.durationMinutes,
    hidden: true,
    scheduleId: eventType.scheduleId,
    beforeEventBufferMinutes: eventType.beforeEventBufferMinutes,
    afterEventBufferMinutes: eventType.afterEventBufferMinutes,
    slotIntervalMinutes: eventType.slotIntervalMinutes,
    minimumBookingNoticeMinutes: eventType.minimumBookingNoticeMinutes,
    locationType: eventType.locationType,
    locationValue: eventType.locationValue
  };
}

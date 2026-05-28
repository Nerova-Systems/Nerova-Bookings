import { t } from "@lingui/core/macro";

import type { EventType, EventTypePayload, Schedule } from "../schedulingTypes";

export type EventTypeTabName =
  | "setup"
  | "availability"
  | "limits"
  | "advanced"
  | "ai-voice-agent"
  | "apps"
  | "dependencies"
  | "instant-meeting"
  | "recurring"
  | "team"
  | "webhooks"
  | "workflows";

export const eventTypeTabNames: EventTypeTabName[] = [
  "setup",
  "availability",
  "limits",
  "advanced",
  "recurring",
  "team",
  "instant-meeting",
  "ai-voice-agent",
  "workflows",
  "webhooks",
  "apps"
];

export function getEventTypeTabLabel(tabName: EventTypeTabName) {
  switch (tabName) {
    case "setup":
      return t`Basics`;
    case "availability":
      return t`Availability`;
    case "limits":
      return t`Limits`;
    case "advanced":
      return t`Advanced`;
    case "apps":
      return t`Apps`;
    case "workflows":
      return t`Workflows`;
    case "webhooks":
      return t`Webhooks`;
    case "recurring":
      return t`Recurring`;
    case "team":
      return t`Team`;
    case "instant-meeting":
      return t`Instant meeting`;
    case "ai-voice-agent":
      return t`AI voice agent`;
    case "workflows":
      return t`Workflows`;
    case "webhooks":
      return t`Webhooks`;
    case "apps":
      return t`Apps`;
  }
}

export function isEventTypeTabName(value: unknown): value is EventTypeTabName {
  return eventTypeTabNames.includes(value as EventTypeTabName);
}

export function getScheduleName(scheduleId: string, schedules: Schedule[]) {
  return schedules.find((schedule) => schedule.id === scheduleId)?.name ?? t`Schedule unavailable`;
}

export function getEventTypePublicUrl(eventType: Pick<EventType, "slug">, publicHandle?: string | null) {
  const handle = publicHandle?.trim() || "book";
  return `/${handle}/${eventType.slug}`;
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
    locationValue: eventType.locationValue,
    settings: eventType.settings
  };
}

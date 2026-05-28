import { t } from "@lingui/core/macro";

import { type Schemas, WebhookEventType } from "@/shared/lib/api/client";

export type Webhook = Schemas["WebhookResponse"];
export type EventType = Schemas["EventTypeResponse"];
export type ApiValidationError = Schemas["HttpValidationProblemDetails"] | null | undefined;

export function getApiErrorMessages(error: ApiValidationError): string[] {
  return [error?.detail, ...Object.values(error?.errors ?? {}).flat()].filter(
    (value): value is string => typeof value === "string" && value.length > 0
  );
}

/**
 * Order shown in the multi-select. Mirrors the backend `WebhookEventType` enum, grouped by
 * lifecycle stage so the most common subscriptions appear first.
 */
export const WEBHOOK_EVENT_TYPE_ORDER: readonly WebhookEventType[] = [
  WebhookEventType.BookingCreated,
  WebhookEventType.BookingRescheduled,
  WebhookEventType.BookingCancelled,
  WebhookEventType.BookingPaid,
  WebhookEventType.BookingReported,
  WebhookEventType.MeetingStarted,
  WebhookEventType.MeetingEnded,
  WebhookEventType.FormSubmitted,
  WebhookEventType.RecordingReady,
  WebhookEventType.Ping
];

export function getWebhookEventTypeLabel(eventType: WebhookEventType): string {
  switch (eventType) {
    case WebhookEventType.BookingCreated:
      return t`Booking created`;
    case WebhookEventType.BookingRescheduled:
      return t`Booking rescheduled`;
    case WebhookEventType.BookingCancelled:
      return t`Booking cancelled`;
    case WebhookEventType.BookingPaid:
      return t`Booking paid`;
    case WebhookEventType.BookingReported:
      return t`Booking reported`;
    case WebhookEventType.MeetingStarted:
      return t`Meeting started`;
    case WebhookEventType.MeetingEnded:
      return t`Meeting ended`;
    case WebhookEventType.FormSubmitted:
      return t`Form submitted`;
    case WebhookEventType.RecordingReady:
      return t`Recording ready`;
    case WebhookEventType.Ping:
      return t`Ping`;
  }
}

/** Returns true when the target URL parses as a valid absolute http(s) URL. */
export function isValidTargetUrl(value: string): boolean {
  const trimmed = value.trim();
  if (trimmed.length === 0) return false;
  try {
    const parsed = new URL(trimmed);
    return parsed.protocol === "http:" || parsed.protocol === "https:";
  } catch {
    return false;
  }
}

/** Truncates a URL for display in the list view. */
export function truncateUrl(url: string, max = 60): string {
  if (url.length <= max) return url;
  return `${url.slice(0, max - 1)}…`;
}

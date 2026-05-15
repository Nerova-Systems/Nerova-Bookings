# UI Atom And Layout Parity

## Rules

- Preserve Cal.diy admin layout structure and interaction flow unless this spec explicitly replaces it.
- Use Nerova `@repo/ui` primitives; do not edit shared styling primitives for this port without explicit approval.
- Create scheduling-local wrappers in `application/main/WebApp` when Cal.com atom behavior needs adapter logic.
- Atom behavior from `packages/platform/atoms` and UI behavior from `packages/ui`/`packages/coss-ui` take priority until Nerova has equivalent scheduling UX.
- Public web booking pages, booking embeds, and Booker public UI are replaced by WhatsApp Flow, but their behavior still informs backend states and WhatsApp copy/steps.

## Source UI Areas

| Source | Classification | Target |
| --- | --- | --- |
| `apps/web/app/(use-page-wrapper)` | include/adapt | Authenticated admin shell and routes in `application/main/WebApp`. |
| `apps/web/app/(booking-page-wrapper)` | replace | Behavior source for WhatsApp Flow only. |
| `apps/web/app/reschedule` | replace | Reschedule behavior through WhatsApp and admin views. |
| `apps/web/components/apps` | include | Connector tile/install UI states. |
| `apps/web/components/booking` | replace/adapt | Admin booking state and behavioral reference; public Booker not exposed. |
| `apps/web/components/eventtype` | include | Event type editor layout reference. |
| `apps/web/components/integrations` | include | App-store/connector layout reference. |
| `apps/web/components/layouts` | include | Admin layout reference. |
| `packages/platform/atoms/availability` | include | Availability editor behavior reference. |
| `packages/platform/atoms/booker` | replace | Public booking behavior reference only. |
| `packages/platform/atoms/booker-embed` | defer | Embed out of v1. |
| `packages/platform/atoms/calendar-settings` | include | Calendar settings behavior reference. |
| `packages/platform/atoms/destination-calendar` | include | Destination calendar selector behavior reference. |
| `packages/platform/atoms/troubleshooter` | adapt/defer | Diagnostics reference; not required for first booking flow. |
| `packages/ui/components` | adapt | Behavior/reference for primitives; do not wholesale copy styling system. |
| `packages/coss-ui` | adapt | Migration/style intent reference only. |

## Required UI Screens

- Solo onboarding entry into scheduling setup.
- Event type list, create, duplicate, edit, disable/delete where Cal.diy supports it.
- Event type tabs: basic details, duration, locations, availability, limits, advanced/custom inputs, destination calendar, conferencing provider.
- Availability schedules: list, create, edit weekly hours, date overrides, timezone, default schedule.
- Bookings admin: upcoming/past/cancelled, detail sheet, confirm/reject, reschedule/cancel, attendee/reference/audit visibility.
- Apps/connectors: app-store list, connector detail, install/uninstall, reconnect, credential error states.
- Calendar settings: selected calendars, destination calendar, provider status.
- WhatsApp connector setup: business account metadata, webhook/Flow status, test flow state, event location option.

## Visual Parity Gate

Each frontend implementation issue must attach or reference Cal.diy source paths, Nerova target paths, desktop and mobile screenshots, loading/empty/error/disabled states, connector installed/uninstalled/reconnect states, and accepted deviations. Accepted deviations are limited to Nerova shell/branding, WhatsApp public booking replacement, and `@repo/ui` primitive styling.

# Connector Scope

## Visible Solo Connectors

| Slug | Tile | Files | Tests | Path |
| --- | --- | --- | --- | --- |
| googlecalendar | Google Calendar | 24 | 5 | cal.diy/packages/app-store/googlecalendar |
| googlevideo | Google Meet | 12 | 0 | cal.diy/packages/app-store/googlevideo |
| office365calendar | Office 365 Calendar | 17 | 0 | cal.diy/packages/app-store/office365calendar |
| office365video | Microsoft Teams | 21 | 1 | cal.diy/packages/app-store/office365video |
| zoomvideo | Zoom | 20 | 1 | cal.diy/packages/app-store/zoomvideo |
| whatsapp | WhatsApp | 11 | 0 | cal.diy/packages/app-store/whatsapp |

## Connector Decisions

| Cal.diy source | Solo tile | Target behavior | Notes |
| --- | --- | --- | --- |
| `packages/app-store/googlecalendar` | Google Calendar | Include | OAuth install, token refresh, selected calendars, free/busy, destination calendar write, calendar event CRUD. |
| `packages/app-store/googlevideo` | Google Meet | Include | Conferencing provider for event locations; reuse Google credential where Cal.diy does. |
| `packages/app-store/office365calendar` | Office 365 Calendar | Include | Microsoft OAuth, Graph calendar free/busy, selected/destination calendar behavior. |
| `packages/app-store/office365video` | Microsoft Teams | Include | Source package slug is `msteams`; expose tile as Microsoft Teams. |
| `packages/app-store/zoomvideo` | Zoom | Include | OAuth install, meeting create/update/delete, credential refresh and failure states. |
| `packages/app-store/whatsapp` | WhatsApp | Adapt | Static `wa.me` location source only; fold into native WhatsApp Business Flow connector. |

## Out Of V1

All other app-store packages are deferred unless their generic infrastructure is required by included connectors. This includes CRM, analytics, payments, messaging, website, automation, and non-selected calendar/video providers.

## App-Store Infrastructure To Port

- Metadata registry and generated registry behavior from app-store generated metadata.
- Shared app-store components, utilities, templates, credential schema patterns, install/uninstall semantics.
- App-store CLI create/edit/delete/generate intent from `packages/app-store-cli` as a Nerova connector scaffolding workflow.
- Connector categories, provider types, app slug/type naming, credential validation, setup URL/callback behavior.

## WhatsApp Business Flow Connector

Target connector responsibilities:

- Verify Meta webhook subscription challenge.
- Validate signed callbacks where applicable.
- Serve fixed WhatsApp Flow data exchanges with no AI decisioning.
- Return event type choices, available dates, available slots, attendee field prompts, confirmation/reschedule/cancel states.
- Reserve slots before final booking and reject stale selections.
- Deduplicate callbacks using provider message/flow identifiers.
- Persist connector credential state and phone/business account metadata.
- Trigger booking lifecycle, notifications, webhooks, and audit events through the same backend services as admin-created bookings.
- Support optional static `wa.me` location per event type using Cal.diy source validation as compatibility behavior.

## Connector Test Requirements

- OAuth callback success/failure, missing state, token refresh, revoked credentials.
- Calendar free/busy provider failures and partial outages.
- Destination calendar write/update/delete failures and retry behavior.
- Conferencing link create/update/delete failures.
- Connector tile states: unavailable, not installed, installed, reconnect required, error.
- WhatsApp webhook verification, version mismatch, invalid connector state, duplicate callbacks, stale slot, happy path, reschedule, cancel.

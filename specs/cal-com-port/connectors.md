# Connectors

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Source Policy

Cal.com is the primary source for connector metadata, app-store behavior, install/uninstall flows, credential handling, OAuth refresh behavior, connector state, and app-store admin UX. Cal.diy may be used only as a smaller comparison reference.

## Visible Solo Connector Scope

| Source slug | Solo tile | Target behavior |
| --- | --- | --- |
| `googlecalendar` | Google Calendar | Calendar read/write, free/busy, selected calendars, destination calendar, calendar subscription where required. |
| `googlevideo` | Google Meet | Conferencing location provider using Google credential patterns. |
| `office365calendar` | Office 365 Calendar | Microsoft calendar read/write, free/busy, selected calendars, destination calendar, subscription behavior where required. |
| `office365video` / `msteams` | Microsoft Teams | Conferencing location provider using Microsoft credential patterns. |
| `zoomvideo` | Zoom | Conferencing provider, OAuth setup, reconnect, and meeting creation behavior. |
| `whatsapp` | WhatsApp | Native Nerova WhatsApp Business Flow connector for public booking and optional static location compatibility. |

## Out Of V1

The following are out of v1 unless required to implement shared app-store infrastructure:

- Gmail
- Drive
- OneDrive
- SharePoint
- Outlook Mail
- Teams chat
- CRM apps
- Analytics apps
- Payment apps
- Other Cal.com app-store integrations

## WhatsApp Handling

WhatsApp is not a static app tile copied from Cal.com. Build a native WhatsApp Business Flow connector that:

- Verifies Meta webhook challenges.
- Handles fixed Flow data exchange with no AI behavior.
- Uses Cal.com booking and slot semantics underneath.
- Rejects duplicate callbacks idempotently.
- Rejects stale slot selections.
- Reports invalid connector state clearly.
- Folds static WhatsApp `wa.me` event location behavior into the connector as optional compatibility.

## Target Connector Platform

The Nerova connector platform should preserve these Cal.com patterns where applicable:

- App metadata registry.
- Categories and visible tiles.
- Install, uninstall, reconnect, enabled, disabled, and error states.
- Credential storage and refresh lifecycle.
- Per-user and future team/org ownership seams.
- Event type app settings.
- Conferencing provider selection.
- Calendar provider selection.
- Background sync, cleanup, and webhook handling.

Every connector implementation issue must cite exact Cal.com source paths, tests, app metadata, API routes, UI components, credential behavior, and target Nerova ownership.

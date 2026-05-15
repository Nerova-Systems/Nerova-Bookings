# Connectors And WhatsApp

This document defines the connector subset and WhatsApp replacement behavior for Solo v1.

## Visible App-Store Tiles

| Tile | Cal.diy source | Status | Notes |
| --- | --- | --- | --- |
| `google-calendar` | `packages/app-store/googlecalendar` | Port | OAuth calendar connector. Free/busy, calendar list, selected calendar, destination calendar, event create/update/delete. |
| `google-meet` | `packages/app-store/googlevideo` | Port | Conferencing location. Depends on Google Calendar and creates Meet links through calendar event conference data. |
| `office365-calendar` | `packages/app-store/office365calendar` | Port | Microsoft Graph calendar connector. Free/busy, calendar list, selected/destination calendars, event create/update/delete. |
| `msteams` | `packages/app-store/office365video` | Port | Microsoft Teams conferencing. Requires Microsoft work/school account behavior from source. |
| `zoom` | `packages/app-store/zoomvideo` | Port | Zoom OAuth conferencing. Meeting create/update/delete and user settings behavior. |
| `whatsapp` | `packages/app-store/whatsapp` plus custom connector | Replace/Port | Native WhatsApp Business Flow connector. Static `wa.me` location behavior folded in as an optional location capability. |

## Out Of V1 Connector Scope

Do not port these as visible Solo v1 tiles: Gmail, Google Drive, OneDrive, SharePoint, Outlook Mail, Teams chat, CRM apps, analytics apps, payment apps, AI-agent apps, and broad marketplace integrations.

Other Cal.diy app-store entries remain classified in `01-source-inventory.md` as references, deferred connectors, or rejected runtime surfaces.

## Shared Connector Contract

Each included connector needs:

- Code-defined app definition with slug, name, category, variant, dependency metadata, required config, and visible plan availability.
- Setup route and app-store tile.
- Backend connection record with tenant ID, owner user/provider ID, connector slug, installed state, health, scopes, provider account ID, and timestamps.
- Encrypted credential record for tokens/secrets.
- OAuth authorize endpoint.
- OAuth callback endpoint with state validation.
- Disconnect endpoint.
- Health check or connection verification action.
- Provider action interfaces for free/busy, event write, meeting creation, update, delete, and webhook processing where applicable.
- Tests for missing config, invalid OAuth state, missing scopes, token refresh, revoked credentials, provider errors, and dependency checks.

## Google Calendar

Source behavior:

- OAuth scopes include Google user profile and calendar event/read-only scopes.
- Callback validates granted scopes and creates the credential.
- Primary calendar is discovered after install.
- Google system calendars that do not return useful free/busy data are filtered.
- Free/busy queries are used for conflict checks.
- Event insert/update/delete creates calendar references.

Nerova behavior:

- Reuse account Google login only as a security pattern; scheduling calendar OAuth is a connector install, not sign-in.
- Store selected calendars and destination calendar separately.
- Google Meet depends on this connection.
- Token refresh and access-denied errors update connection health.

## Google Meet

Source behavior:

- Not independently OAuth'd.
- Depends on Google Calendar.
- Adds conference data to Google Calendar events.

Nerova behavior:

- Expose a separate app-store tile like Cal.diy.
- Installation checks Google Calendar dependency.
- Event type location uses `integrations:google:meet`.
- Booking creation requests Meet link through the Google Calendar event write path.

## Outlook Calendar

Source behavior:

- OAuth scopes include `User.Read`, `Calendars.Read`, `Calendars.ReadWrite`, and `offline_access`.
- Callback discovers Graph user email and default calendar.
- Free/busy and event writes use Microsoft Graph.
- Timezone conversion uses Windows/IANA mapping.

Nerova behavior:

- Store Microsoft account identity and default calendar.
- Support selected calendars and destination calendar.
- Preserve timezone conversion and Graph paging behavior.
- Token refresh and scope errors update connection health.

## Microsoft Teams

Source behavior:

- Separate app-store tile: `msteams`.
- Uses `OnlineMeetings.ReadWrite` and `offline_access`.
- May use delegated Microsoft Graph behavior.
- Source explicitly says work/school account is required.

Nerova behavior:

- Expose as separate tile.
- Surface work/school account requirement in setup UI.
- Meeting creation writes booking reference with join URL.
- If Teams location is selected but connector is unhealthy, booking must fail deterministically or require another location before publication.

## Zoom

Source behavior:

- OAuth authorize/callback.
- Token refresh through Zoom token endpoint.
- Reads user meeting settings.
- Creates scheduled meetings, deletes meetings, updates meetings, and can read scheduled meetings.
- Handles invalid token/provider errors.

Nerova behavior:

- Expose Zoom tile.
- Store Zoom account and token health.
- Preserve meeting password/waiting-room/user settings behavior where source tests cover it.
- Booking side effects create/update/delete Zoom meeting references idempotently.

## WhatsApp Business Flow Connector

Source inputs:

- Cal.diy minimal `whatsapp` app is static: no auth, messaging category, `wa.me` URL input, event location type `integrations:whatsapp_video`.
- Existing Nerova Facebook Login for Business code is a security/OAuth pattern for Meta business authorization, not a complete WhatsApp connector.

Nerova connector:

- One visible `whatsapp` tile.
- Native Meta WhatsApp Business/Cloud API connector.
- Stores WABA ID, phone number ID, Graph API version, business/account identifiers, app secret, webhook verify token, encrypted access token, Flow ID, repo Flow version, published Flow version, and health state.
- Supports Meta webhook verification and signed event handling.
- Supports Flow data exchange endpoint.
- Supports deterministic outbound messages for confirmation, errors, reminders, reschedule/cancel links or Flow actions.
- Supports optional `wa.me` static location capability for event types where the business wants a WhatsApp call/chat link.

## WhatsApp Flow Version Policy

- Flow JSON lives in the repo as the canonical definition.
- Developer tooling validates the JSON before publish.
- Publish tooling records Flow ID and published version.
- Runtime refuses customer booking when connector Flow version does not match repo version.
- Tests assert screen/action names and backend payload contracts.

## WhatsApp Booking Screens

Minimum Flow screens:

- Choose service/event type.
- Choose date.
- Choose slot.
- Enter attendee details.
- Answer custom booking questions.
- Review booking details.
- Confirm booking.
- Success.
- Deterministic error states: no availability, stale slot, invalid connector, invalid Flow version, non-Solo tenant, disabled service, provider failure.

## Connector Acceptance

A connector is accepted only when:

- App-store tile state, setup, installed state, disconnected state, and unhealthy state are visible.
- Backend rejects use when dependency or health requirements fail.
- Credentials are encrypted and hidden from frontend.
- OAuth and webhook handlers are covered by tests.
- Booking side effects are idempotent.
- Provider failures are reported in admin UI and do not silently create partial bookings.


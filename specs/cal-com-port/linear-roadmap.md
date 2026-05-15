# Linear Roadmap

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Creation Timing

Linear comes after Cal.com source inventory and Cal.com test traceability are regenerated. Do not create implementation work from memory, from Cal.diy assumptions, or from the superseded inventory tables.

## Initiative

- `Cal.com Port to NerovaBookings`

## Projects

| Project | Purpose | Completion Evidence |
| --- | --- | --- |
| Cal.com Source Inventory & Upstream Diff | Keep this spec current against the Cal.com checkout. | Updated inventory, test traceability, source snapshot, and diff notes. |
| Solo Tier & Access Gating | Add Solo product eligibility around existing Nerova account/subscription/feature flag systems. | Solo owner-only guard behavior and frontend/runtime visibility checks. |
| Scheduling Core | Port Cal.com core scheduling semantics. | Parity tests for availability, busy-time merge, slots, reservations, and stale-slot rejection. |
| Event Types & Availability | Port event type and availability admin behavior. | API, UI, persistence, and E2E parity for Solo event setup. |
| Booking Lifecycle | Port booking creation and lifecycle behavior. | Create, confirm, reject, reschedule, cancel, no-show, audit, notification, and race tests. |
| App Store & Connector Platform | Port app-store registry, credentials, install/uninstall, and connector state patterns. | Connector platform tests and admin setup flows. |
| Google, Microsoft & Zoom Connectors | Implement visible workforce connectors. | Google Calendar, Google Meet, Office 365 Calendar, Microsoft Teams, and Zoom behavior tests. |
| WhatsApp Flow Booking | Replace Solo public web booking with WhatsApp Flow callbacks. | Webhook verification, Flow data exchange, duplicate callback, stale slot, and happy path E2E tests. |
| Admin Web UI & Atom Parity | Port authenticated Cal.com admin layouts and atom behavior into Nerova React. | Desktop/mobile screenshots, loading/empty/error states, and accepted deviations. |
| Notifications, Webhooks & Background Tasks | Port scheduling-related background semantics. | Retry, delayed task, cleanup, notification, and webhook delivery tests. |
| Test Parity, E2E & Release Hardening | Translate and expand Cal.com tests. | Parity matrix closed and release validation evidence attached. |

## Implementation Task Template

Each Linear task must include:

- Cal.com source paths and tests.
- Spec section references from `specs/cal-com-port`.
- Target Nerova files.
- Included, adapted, replaced, deferred, and out-of-scope behavior.
- Data model, API, frontend, and generated client changes.
- Required tests and E2E coverage.
- UI parity requirements when frontend is touched.
- Shared styling lock statement.
- Agent ownership and reviewer ownership.
- Guardian validation evidence.

## Labels

- `cal-com-port`
- `source-inventory`
- `scheduling-core`
- `event-types`
- `availability`
- `bookings`
- `connectors`
- `whatsapp-flow`
- `admin-ui`
- `atom-parity`
- `background-tasks`
- `test-parity`
- `solo`
- `teams-seam`
- `org-seam`

## First Linear Batch

1. Regenerate Cal.com source inventory.
2. Regenerate Cal.com test traceability.
3. Produce Cal.com vs Cal.diy delta appendix.
4. Write scheduling foundation implementation plan.
5. Create first implementation wave only after those documents are current.

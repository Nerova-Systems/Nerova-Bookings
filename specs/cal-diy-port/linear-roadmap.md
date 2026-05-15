# Linear Roadmap

## Creation Timing

Linear is generated after this repo-local inventory exists. Do not create implementation work from memory or from stale Cal.diy assumptions.

## Initiative

- `Cal.diy Port to NerovaBookings Solo`

## Projects

| Project | Purpose | First output |
| --- | --- | --- |
| Cal.diy Source Inventory & Upstream Diff | Keep this spec current after each upstream pull. | Updated inventory, diff notes, changed classifications. |
| Solo Tier & Access Gating | Add account plan/capability gating. | Solo eligibility visible to backend/frontend. |
| Scheduling Core | Port schedules, availability, timezone, OOO, busy-time primitives. | Domain tests passing for availability math. |
| Event Types & Availability | Port event type admin and availability editing. | Event type CRUD plus availability UI. |
| Booking Lifecycle | Port booking states and references. | Booking create/reschedule/cancel/confirm/reject. |
| App Store & Connector Platform | Port registry, credentials, install states, CLI intent. | Connector registry and app-store UI foundation. |
| Google, Microsoft & Zoom Connectors | Port workforce calendar/video connectors. | Calendar free/busy and conferencing links working. |
| WhatsApp Flow Booking | Replace public booking UX. | End-to-end WhatsApp happy path and failure handling. |
| Admin Web UI & Atom Parity | Port authenticated UI layouts and atom behavior. | Visual parity evidence for admin flows. |
| Notifications, Webhooks & Background Tasks | Port async semantics. | Retryable jobs, notifications, webhook delivery. |
| Test Parity, E2E & Release Hardening | Close gaps and prepare release. | Full gate passing with traceability complete. |

## Implementation Task Template

Each Linear task must include Cal.diy source paths, spec section references, target Nerova files, included/excluded behavior, data model/API/frontend changes, required tests, UI parity requirements when frontend is touched, the shared styling lock statement, agent ownership, and completion gate evidence.

## Labels

- `area:backend`
- `area:frontend`
- `area:qa`
- `area:connector`
- `area:whatsapp`
- `area:ui-parity`
- `source:apps-api`
- `source:apps-web`
- `source:packages`
- `source:tests`
- `gate:guardian`
- `gate:e2e`
- `gate:visual-parity`

## First Linear Batch

1. Update Cal.diy inventory after upstream pull.
2. Add Solo plan/capability gate.
3. Port connector registry metadata foundation.
4. Port event type and availability domain skeleton.
5. Translate slot algorithm tests.
6. Build WhatsApp Flow endpoint contract tests.

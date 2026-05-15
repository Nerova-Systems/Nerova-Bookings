# Cal.com Port Design

## Overview

Port Cal.com into NerovaBookings as a native feature-sliced implementation. Cal.com is the primary source of truth for scheduling, bookings, event types, availability, app-store/connectors, admin UX layouts, atom behavior, team/organization seams, and parity tests.

This is not a runtime transplant. Nerova keeps its existing account, subscription, feature flag, AppGateway, shared UI, OpenAPI, build, test, and deployment systems. Cal.diy remains useful only as a simplified reference when comparing smaller behavior paths.

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Source Snapshot

- Primary source: `cal.com`
- Current Cal.com commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`
- Current Cal.com branch: `fix/team-attributes-for-org-admins`
- Secondary reference: `cal.diy`
- Current Cal.diy commit: `180ede28f0bddf2738933a6e60a8e80f6116d7da`
- Current Cal.diy branch: `main`
- Target runtime: Nerova .NET backend, Postgres persistence, React/Rsbuild frontend, generated OpenAPI clients, TanStack Query, Lingui, and `@repo/ui`.

Before coding, refresh the snapshot and regenerate the Cal.com inventory if either source checkout changes.

## Locked Product Decisions

- Port style: feature-sliced rewrite.
- Source hierarchy: Cal.com primary, Cal.diy secondary comparison, Nerova architecture authoritative.
- First product tier: Solo.
- Solo account shape: one owner user per account.
- Later tiers: Teams introduces one shared team; Organizations introduces multiple teams.
- Pricing reference: align product language with Cal.com Individual/Teams/Organizations concepts while preserving existing Nerova internal plan mechanics.
- First rollout guard: use Nerova feature flags for rollout and kill switch; enforce Solo user limits as product rules, not feature-flag logic.
- Public booking: WhatsApp Flow for Solo.
- Public web booking and embeds: use Cal.com behavior as reference only; do not expose the public Cal.com web booker for Solo.
- Styling: keep Nerova shared styling primitives untouched unless separately approved.
- UI behavior: Cal.com atom behavior and admin layouts have parity priority until Nerova equivalents are complete.

## Architecture

Backend work belongs in `application/main` and follows Nerova SCS conventions. The Cal.com concepts become Nerova domains for scheduling, event types, availability, slots, bookings, attendees, reservations, app-store/connectors, credentials, webhooks, background jobs, notifications, audit, and reporting.

Frontend work belongs in `application/main/WebApp`. Authenticated admin flows should follow Cal.com layout and interaction behavior while using Nerova shell, generated OpenAPI clients, TanStack Query, Lingui, and `@repo/ui`. Cal.com atoms guide behavior and layout; Nerova styling primitives remain the visual base.

Account, identity, plan eligibility, feature flags, and billing/subscription integration remain owned by existing Nerova account systems. The scheduling port consumes those capabilities; it does not replace them.

## Scheduling Foundation

The first implementation wave is strict Cal.com core scheduling parity:

- Event type configuration.
- Availability schedules, working hours, date overrides, out-of-office, holidays where applicable, travel and timezone behavior.
- Selected calendars, destination calendars, conferencing locations, and external busy time lookup.
- Slot generation, busy-time merge, limits, reservation, stale slot rejection, race handling, and idempotency.
- Booking create, confirm, reject, reschedule, cancel, no-show, attendee handling, references, audit semantics, and notification triggers.

Solo-only delivery may hide or defer team/org UI, but data and service boundaries must preserve Cal.com seams for later Teams and Organizations work.

## Connectors

Visible Solo connector scope:

- Google Calendar
- Google Meet
- Office 365 Calendar
- Microsoft Teams
- Zoom
- Native WhatsApp Business Flow

Out of v1 unless shared infrastructure requires a generic pattern:

- Gmail, Drive, OneDrive, SharePoint, Outlook Mail, Teams chat, CRM, analytics, payments, and other Cal.com app-store integrations.

WhatsApp is native Nerova connector work. Cal.com/Cal.diy static WhatsApp location behavior is folded into the connector as optional event location compatibility.

## Future Boundaries

The following systems are acknowledged as part of the larger Nerova product vision but are not implemented during the Cal.com port:

- Meta Business management.
- Client management portal.
- Smart CSV/Excel import and export.
- Loyalty system.
- Dynamic per-appointment rating system.
- Broader smart data and business intelligence.

The Cal.com port should avoid irreversible data decisions that block those systems, but it should not implement speculative client, loyalty, rating, import, export, or Meta Business abstractions before Cal.com parity passes.

## Implementation Readiness

No implementation issue is ready until it includes:

- Exact Cal.com source paths and tests.
- Target Nerova backend, frontend, persistence, API, and test ownership.
- Included, adapted, deferred, and replaced behavior.
- UI parity expectations for frontend work.
- Feature flag and plan-gating expectations.
- Validation commands and expected evidence.

The immediate next step after this rewrite is to regenerate `source-inventory.md` and `test-traceability.md` from Cal.com.

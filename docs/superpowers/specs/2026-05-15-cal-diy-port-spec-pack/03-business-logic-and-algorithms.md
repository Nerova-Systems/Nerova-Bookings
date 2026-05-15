# Business Logic And Algorithms

This document captures the behavior that must survive the rewrite.

## Source Priority

When sources disagree, use this priority:

1. Cal.diy tests that exercise real behavior.
2. Package-level implementation in `packages/features`, `packages/platform/libraries`, `packages/app-store`, and `packages/trpc`.
3. `apps/api/v2` controllers, DTOs, guards, pipes, and services.
4. `apps/web` route loaders, modules, and components.
5. `apps/docs` descriptions.

API v2 is not enough by itself. The dashboard uses tRPC and package services heavily, and public booking behavior is route-loader plus Booker state, not a simple REST contract.

## Event Type And Service Rules

Port these behaviors:

- Title, slug, description, duration, variable durations where included, hidden state, and owner/provider association.
- Location choices: manual link, phone, address, attendee-defined fields, and integration locations for v1 connectors.
- Schedule binding: event type can use default availability or a specific schedule.
- Buffers before/after, minimum booking notice, booking window, slot interval, offset start, optimized/first-slot behavior where tests prove it.
- Booking fields/custom inputs, required flags, phone/email fields, prefill behavior, and response schema validation.
- Cancellation/rescheduling policies and fields that control what a booker can do.
- Private link behavior where needed for invite-only WhatsApp flows or future web fallback.

Defer recurring event types, seats, paid event types, managed/team event types, round-robin, collective, field inheritance, and translations unless a later slice explicitly enables them.

## Availability Rules

Port these behaviors:

- Default schedule per provider.
- Named schedules with time zone.
- Weekly availability windows by weekday and `HH:MM` style local time.
- Date overrides with local-date semantics.
- Time zone conversion between provider availability, viewer/client time zone, and stored UTC ranges.
- Travel schedules, out-of-office, holidays, and date blockers when they affect returned slots.
- Validation for overlapping windows, inverted ranges, empty schedules, invalid days, invalid time formats, and deletion of in-use schedules.

The port must not rely on browser-local time. Backend calculations are authoritative.

## Busy-Time And Slot Algorithms

Port these algorithmic seams:

- Selected calendars are read as conflict sources.
- Destination calendar is the write target after booking creation.
- Busy time combines external calendars, internal bookings, booking limits, selected slots/reservations, out-of-office, holidays, and provider availability.
- Slot generation uses event duration, slot interval, buffers, minimum notice, booking window, event type limits, and timezone-aware schedule windows.
- Calendar-specific exclusions, such as Google system calendars that do not return useful free/busy data, must be preserved.
- Large free/busy requests are chunked where provider APIs require it.
- Existing reservations make slots unavailable during the hold period.
- Stale reservation or stale selected slot is rejected at booking time.
- Confirmed bookings outrank reservations.

Required race behavior:

- Two clients selecting the same slot may both see it initially.
- Only one booking can commit.
- The loser receives a stale/unavailable slot response and must choose another slot.
- WhatsApp Flow callbacks must use the same reservation and stale-slot rules as the web Booker.

## Booking Lifecycle

Port these states and transitions:

- Booking create from a selected event type, time, attendee data, booking fields, location, and selected connector.
- Reschedule with original booking reference, policy checks, old calendar/video cleanup, and new references.
- Cancel with cancellation policy, reason where required, calendar/video cleanup, notification triggers, and webhook triggers.
- Confirm/reject for confirmation-required event types.
- Attendees and guests where included by slice.
- Booking references for external calendars, video meetings, WhatsApp message/Flow interactions, and webhook side effects.
- Booking audit timeline for created, accepted, rejected, cancelled, rescheduled, location changed, attendee added/removed, and no-show changed actions once those actions exist.

Reject for v1:

- Paid booking flows through Cal Stripe.
- AI-assisted booking.
- Wrong-assignment reports for team assignment.

## App-Store Registry Algorithm

Port the app-store shape, not the full app catalog:

1. Code-defined app metadata is the source of truth.
2. Generator validates required fields, slugs, categories, variants, app keys schema, and server/browser handlers.
3. Registry output is deterministic and reviewed in source control.
4. App installation creates tenant/user-scoped connection records.
5. App dependencies are enforced before install or use. Google Meet depends on Google Calendar.
6. Location options come from installed app metadata and event type rules.
7. Visible Solo catalog is filtered to six app tiles.

The Nerova implementation can live in the developer CLI rather than a Node package, but it must preserve deterministic generation, validation, and test coverage.

## Connector Algorithms

Port common connector behavior:

- OAuth state contains return target and CSRF protection.
- Callback validates state, exchanges code, validates required scopes, stores encrypted token response, and creates selected/destination calendar defaults where needed.
- Token refresh is centralized and updates stored token objects atomically.
- Provider errors are classified as transient, expired credential, permission/scope issue, rate limited, or permanent validation failure.
- Connection health is surfaced to admin UI and used by booking side effects.
- Provider actions are idempotent by booking/reference ID.

Provider specifics are in `05-connectors-and-whatsapp.md`.

## WhatsApp Flow Booking Algorithm

The WhatsApp booking flow replaces the public web Booker for Solo customers.

1. Inbound WhatsApp user starts or resumes a deterministic Flow.
2. Backend loads tenant, connector, event types, and Flow version.
3. Flow data exchange returns event types, available dates, or slots depending on screen state.
4. Slot selection reserves the selected slot.
5. Confirmation validates reservation, event type policy, attendee answers, phone/email verification requirements, and connector health.
6. Booking creation commits in a transaction and stores booking references.
7. Outbound messages send confirmation details, calendar/video links, and reschedule/cancel links or Flow actions.
8. Duplicate callbacks return the already-created booking result.
9. Stale slots, invalid Flow version, disabled event type, non-Solo tenant, or disconnected provider return deterministic error screens.

No AI behavior is permitted. The bot is fixed, rich-interface based, and replayable from stored Flow/input data.

## Tasker, Outbox, And Redundancy

Cal.diy uses tasker patterns for non-critical work and retries. Nerova must replace this with a durable outbox/task model:

- Write domain mutation and outbox records transactionally.
- Process side effects asynchronously with idempotency keys.
- Store attempt count, next retry, last error, and final status.
- Support scheduled work for reminders, no-show checks, webhook delivery, and cleanup.
- Cancel scheduled work when bookings are cancelled or rescheduled.
- Keep provider webhook handling thin: verify, persist/enqueue, then process out of band.

Redundancy to preserve:

- API v2 and tRPC often expose overlapping behavior; tests and package services decide the canonical behavior.
- Booking side effects often write calendar, video, notifications, webhooks, audit, and task records. Implementation must not "just create the booking row".
- UI state and backend validation both check eligibility; backend remains authoritative.

## Security And Validation

Required protections:

- Tenant isolation on every query and mutation.
- Backend Solo plan enforcement.
- OAuth state validation and redirect allowlist.
- Encrypted credentials and no token exposure to frontend logs or API responses.
- Provider webhook signature validation.
- WhatsApp webhook verification and signature validation.
- Idempotency on webhook events and Flow callbacks.
- SSRF-safe webhook URL validation copied from Cal.diy tests and adapted to .NET.
- PII-safe logging and audit records.


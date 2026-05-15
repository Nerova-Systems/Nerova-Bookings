# Test And Traceability

This document defines how Cal.diy tests and source areas become Nerova acceptance tests.

## Inventory Commands

Run these after the latest Cal.diy pull and before implementation:

```powershell
rg --files 'cal.diy/apps' | Measure-Object | Select-Object -ExpandProperty Count
rg --files 'cal.diy/packages' | Measure-Object | Select-Object -ExpandProperty Count
Get-ChildItem -Path 'cal.diy/apps/api/v2/src/modules' -Directory | Select-Object -ExpandProperty Name
Get-ChildItem -Path 'cal.diy/apps/web/modules' -Directory | Select-Object -ExpandProperty Name
Get-ChildItem -Path 'cal.diy/packages' -Directory | Select-Object -ExpandProperty Name
Get-ChildItem -Path 'cal.diy/packages/app-store' -Directory | Select-Object -ExpandProperty Name
rg --files 'cal.diy/apps' 'cal.diy/packages' | rg '(test|spec|e2e|__tests__|playwright)'
rg -n '^(model|enum) ' 'cal.diy/packages/prisma/schema.prisma'
```

If any count, module, app-store entry, Prisma model, or test area changes, update this spec pack before coding.

## Source Test Inventory

The local source contains test coverage across these broad areas:

- `apps/api/v2` e2e specs, controller specs, guards, pipes, pagination, origin checks, auth strategies, OAuth flows, slots, schedules, event types, bookings, calendars, webhooks, conferencing, selected/destination calendars, verified resources, and unified calendars.
- `apps/web/test` unit tests for schedule calculations, timezone helpers, booking limits, rewrite paths, and selected slots.
- `apps/web/playwright` e2e tests for signup, login, OAuth, event types, availability, booking pages, booking limits, booking seats, reschedule, payment, profile, settings, out-of-office, webhooks, app-store, conferencing apps, embeds, and public booking flows.
- `apps/web/modules` component and hook tests for bookings, schedules, event types, form builder, users, shell, timezone, videos, apps, and data table behavior.
- `packages/features` tests for availability, busy times, calendar subscriptions, bookings, booking audit, booking reports, event type translations, feature flags, forms, holidays, host, profile, tasker, watchlist, webhooks, and no-show behavior.
- `packages/app-store` tests for app metadata, app dependencies, default locations, OAuth manager, provider adapters, Google Calendar, Office365 video, Zoom video, Stripe, Salesforce, HubSpot, and shared utilities.
- `packages/platform` tests for atoms, event type duration formatting, permissions, and API utilities.
- `packages/trpc` tests for dashboard handlers and error formatting.
- `packages/embeds` tests for core embed, iframe, React package, namespacing, inline, action-based, preview, and slot selection behavior.
- `packages/ui` tests for UI primitives and higher-level components.
- `packages/lib`, `emails`, `sms`, `i18n`, `prisma`, and `testing` tests for utilities, generated ICS, tasker, PKCE, SSRF protection, slugify, sensitive-data redaction, localized behavior, mock fixtures, and performance scenarios.

## Test Mapping Statuses

Each Cal.diy test file must be assigned one status in implementation planning:

- `Port equivalent`: behavior is included and needs an equivalent .NET, frontend unit, or Playwright test.
- `Replace equivalent`: behavior is included but through Nerova architecture, so the assertion changes but the intent remains.
- `Reference only`: use as behavior context but not a direct acceptance case.
- `Defer`: feature is out of v1 and test is recorded for a future slice.
- `Not applicable`: Cal runtime/framework behavior rejected by this port.

## Required Nerova Test Suites

Backend/domain tests:

- Solo tier gating for all scheduling endpoints.
- Event type create/list/get/update/delete/duplicate/hide behavior.
- Slug uniqueness and validation.
- Schedule defaults, weekly windows, overrides, timezone validation, and deletion rules.
- Busy-time aggregation from internal bookings, selected calendars, OOO, holidays, and booking limits.
- Slot generation, reservation, expiry, stale reservation rejection, and concurrent booking race.
- Booking create/reschedule/cancel/confirm/reject lifecycle.
- Calendar selected/destination behavior.
- Google, Microsoft, Zoom credential and provider error behavior.
- App-store registry and dependency validation.
- Webhook URL validation, payload generation, retry, idempotency, and failure states.
- WhatsApp webhook verification, Flow data exchange, version mismatch, stale slot, duplicate callback, and successful booking.

Frontend tests:

- Solo navigation visibility and non-Solo denial.
- Event type list/editor tabs and validation states.
- Availability schedule editor and overrides.
- Bookings list/detail/actions.
- App-store tile filtering, setup, installed/unhealthy/disconnected states.
- Calendar and conferencing settings.
- WhatsApp connector setup and Flow health state.
- Loading, empty, pending, disabled, validation, and error states.

End-to-end tests:

- Solo onboarding through first event type and schedule.
- Google Calendar install and selected/destination calendar setup with mocked provider.
- Microsoft Calendar/Teams install path with mocked provider.
- Zoom install path with mocked provider.
- WhatsApp connector setup with mocked Meta APIs.
- WhatsApp Flow booking happy path.
- WhatsApp stale slot and duplicate callback scenarios.
- Booking reschedule/cancel after WhatsApp-created booking.
- Non-Solo tenant cannot access scheduling routes or APIs.

## Traceability Template

Every implementation task must include this table:

| Field | Required content |
| --- | --- |
| Cal.diy source | Exact file, directory, or test path. |
| Source status | Port, Replace, Reference, Defer, or Reject. |
| Nerova target | Exact backend/frontend/test area. |
| Behavior copied | One sentence naming the rule/workflow. |
| Known deviation | Why Nerova differs, if it differs. |
| Tests | Exact new or updated test names. |

## Verification Commands

Use developer CLI skills for actual implementation verification. For this docs-only spec pack, the minimum verification is:

```powershell
git diff -- docs/superpowers/specs/2026-05-15-cal-diy-port-spec-pack
rg -n 'T[B]D|T[O]DO|implement l[a]ter|fill in d[e]tails' 'docs/superpowers/specs/2026-05-15-cal-diy-port-spec-pack'
```

For code implementation later, use the project workflow:

- Build first.
- Then run format, lint, and tests with `--no-build` where supported.
- Use the Aspire restart skill only when AppHost restart is required.
- Run e2e through the e2e skill.

## Acceptance Checklist

- [ ] No Cal.diy app, package, API module, web module, connector, or test area is unclassified.
- [ ] Every included source area names a target Nerova subsystem.
- [ ] Every deferred or rejected source area has a reason.
- [ ] Public Solo web booking is replaced by WhatsApp Flow.
- [ ] The visible app-store catalog is limited to the six approved tiles.
- [ ] Connector dependencies are explicit.
- [ ] Business logic includes slots, booking lifecycle, reservations, selected calendars, destination calendars, webhooks, tasks, notifications, and audit behavior.
- [ ] UI atom coverage is mapped to `@repo/ui`.
- [ ] Implementation planning requires source traceability per task.

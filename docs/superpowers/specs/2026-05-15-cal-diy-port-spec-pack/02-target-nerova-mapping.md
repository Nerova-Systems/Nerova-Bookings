# Target Nerova Mapping

This document maps Cal.diy source concepts into NerovaBookings architecture. The goal is source-faithful behavior with native Nerova structure.

## Architecture Boundary

| Cal.diy source concept | Nerova target | Notes |
| --- | --- | --- |
| Next.js web runtime | React/Rsbuild SPA served by .NET | Preserve workflows/layouts, not framework mechanics. |
| Nest API v2 | .NET Minimal APIs/CQRS handlers | Preserve endpoint behavior, validation, status/error contracts where useful. |
| tRPC dashboard routers | .NET endpoints plus generated OpenAPI client | Treat tRPC as dashboard behavior oracle. |
| Prisma schema | EF Core/Postgres model | Use Prisma model names and relations as source data dictionary only. |
| Cal app-store packages | Nerova integration registry and developer CLI generator | Filter visible catalog to Solo v1 connectors. |
| Cal atoms/platform packages | `@repo/ui` components and generated API clients | Preserve public contracts and workflow capabilities where included. |
| Cal tasker | Nerova durable task/outbox model | Preserve retry, scheduled execution, cancellation, and cleanup semantics. |
| Cal auth/session/OAuth platform | Account SCS auth plus connector OAuth services | Do not import NextAuth or platform OAuth runtime. |

## Account SCS Changes

| Requirement | Target |
| --- | --- |
| Add Solo eligibility | Add `Solo` to subscription plan domain, pricing catalog, plan ordering, billing events, back-office plan display, and frontend plan utilities. |
| Runtime guards | Inject `subscriptionPlan` into user info/runtime metadata if missing and expose a canonical `isSoloSchedulingEligible` helper. |
| Access denial | Non-Solo tenants cannot see or call Solo scheduling/admin endpoints. Backend enforces; frontend hides and handles denial. |
| Billing | Keep Paystack/account billing as source of truth. Do not port Cal Stripe payment logic. |

## Main SCS Backend Domains

| Domain | Cal.diy sources | Nerova responsibilities |
| --- | --- | --- |
| Scheduling catalog | Event type APIs, `features/eventtypes`, web event-type modules | Service/event type aggregate, slug rules, descriptions, durations, buffers, booking windows, limits, hidden state, booking fields, location policies. |
| Availability | Schedule APIs, `features/schedules`, `features/availability`, schedule editor | Availability schedule aggregate, weekly windows, overrides, timezone handling, default schedule behavior. |
| Busy time and calendars | Calendar APIs, selected/destination calendar modules, app-store calendar adapters | Selected calendars as conflict sources, destination calendar as write target, provider credential health, free/busy fetches. |
| Slots | Slots API versions, `features/slots`, `features/bookings/Booker`, web slot tests | Slot generation, month/date range lookup, reservation, stale slot rejection, conflict checks, timezone projection. |
| Bookings | Booking API versions, `features/bookings`, booking web modules | Booking aggregate, attendees, references, create/reschedule/cancel/confirm/reject, admin list/detail, audit events. |
| App store | `packages/app-store`, `packages/app-store-cli`, web apps module | App definitions, app registry, connector install/uninstall, dependency checks, static metadata, setup UI, generator/validator. |
| Connectors | Google, Microsoft, Zoom, WhatsApp app-store folders and API modules | OAuth flows, encrypted credentials, token refresh, provider calls, connection health, provider-specific retries. |
| WhatsApp booking | Custom requirement plus Cal booking state machine | Meta webhook verification, Flow data exchange, deterministic booking conversation, Flow version enforcement, idempotent callbacks. |
| Webhooks | API/webhook modules, `features/webhooks`, tasker tests | User/event-type booking webhooks, payload builders, retries, scheduled delivery, validation, audit. |
| Notifications | `emails`, `sms`, booking task services | Booking confirmation/reminder/cancel/reschedule notifications through Nerova email/WhatsApp infrastructure. |
| Audit/reporting | Booking audit/report packages, booking admin modules | Booking audit timeline, report reasons/status, operational visibility. |

## Data Model Mapping

| Cal.diy model group | Status | Nerova target |
| --- | --- | --- |
| `User`, `Profile`, `Membership`, `Team`, `OrganizationSettings` | Replace/Reference | Account users remain in account SCS. Main gets provider/staff profile records tied to tenant/user. Team/org behavior deferred. |
| `EventType`, `EventTypeCustomInput`, `EventTypeTranslation`, `Host`, `HostLocation`, `HostGroup` | Port/Filter | Event type/service aggregate. Custom fields port. Translation, host groups, managed/team features deferred. |
| `Schedule`, `Availability`, `TravelSchedule`, `OutOfOfficeEntry`, `HolidayCache`, `UserHolidaySettings` | Port | Availability schedules, overrides, travel/OOO/holiday blockers where behavior impacts slots. |
| `SelectedCalendar`, `DestinationCalendar`, `CalendarCache`, `CalendarCacheEvent` | Port/Filter | Selected/destination calendars. Cache/sync deferred until provider sync is implemented. |
| `Credential`, `App`, `WorkspacePlatform`, `DelegationCredential` | Replace/Filter | Nerova integration connection and encrypted credential tables. Workspace delegation deferred except where Google/Microsoft need it later. |
| `Booking`, `Attendee`, `BookingReference`, `BookingSeat`, `Tracking`, `SelectedSlots` | Port/Filter | Booking aggregate, attendees, external references, reservations. Seats deferred unless explicitly enabled. |
| `Webhook`, `WebhookScheduledTriggers` | Port | External webhook subscriptions and scheduled dispatch. |
| `ApiKey`, `OAuthClient`, `AccessCode`, `PlatformOAuthClient`, `AccessToken`, `RefreshToken` | Defer/Replace | Existing account auth remains. API/platform OAuth is future platform work. |
| `Payment`, billing models, credits | Reject/Defer | Cal payments are out of Solo v1; account SCS owns billing. |
| `Feature`, `UserFeatures`, `TeamFeatures`, filter segments | Replace/Reference | Use Nerova plan/feature flags and UI filters. |
| `Watchlist`, blocklist, wrong assignment, assignment reason | Defer/Reference | Future abuse/team assignment features. |
| `Task` | Port/Replace | Durable task/outbox table with retry and status semantics. |
| `BookingAudit`, `AuditActor`, `InternalNotePreset`, `BookingInternalNote`, booking reports | Port/Filter | Booking audit and reporting after booking lifecycle exists. |
| AI/phone-number models | Defer/Reject | No AI in WhatsApp Flow bot. |

## API Surface

Nerova should expose first-party endpoints under the `main` SCS. Exact route names are implementation details, but these surfaces must exist:

- Event types/services: create, list, get, update, duplicate, hide/show, delete, validate slug, manage booking fields, manage locations.
- Schedules: create, list, get default, update, delete, set default, manage weekly windows, manage date overrides.
- Slots: get slot ranges, reserve slot, check reserved slot, release/expire reservation.
- Bookings: create from WhatsApp Flow, admin list, get detail, reschedule, cancel, confirm/reject, update location, manage attendees where enabled.
- Calendars: OAuth authorize/callback, list connected calendars, select calendars, set destination calendar, disconnect.
- Conferencing: OAuth authorize/callback, list apps, set default provider, disconnect, create/update/delete meeting reference through booking side effects.
- App store: list visible apps, get app details, install, configure, uninstall, validate dependencies, get setup status.
- WhatsApp: embedded signup/OAuth callback where applicable, webhook verification, inbound events, Flow data exchange, connection health, publish-version check.
- Webhooks: CRUD subscriptions, test trigger, delivery status if booking lifecycle events are enabled.

All frontend calls go through generated clients. No direct browser `fetch` calls.

## Frontend Mapping

| Cal.diy UI source | Nerova target |
| --- | --- |
| Event type list/editor | Main scheduling/service routes. |
| Availability list/editor | Main availability routes. |
| Bookings list/detail/sheet | Main bookings dashboard. |
| Apps homepage/category/installed/setup | Main integrations/app-store routes filtered to v1 catalog. |
| Calendar settings | Main integrations settings for selected/destination calendars. |
| Conferencing settings | Main integrations settings for Meet/Teams/Zoom. |
| Webhook settings | Main developer/integrations settings after booking webhooks exist. |
| Public Booker page | Replaced by WhatsApp Flow; admin/internal Booker state remains a reference. |
| Embed pages/SDK | Deferred; atoms still inventoried. |
| Auth/account/settings unrelated to scheduling | Account SCS retains ownership. |

## Persistence Rules

- Use strongly typed IDs following existing Nerova conventions.
- Keep tenant ownership on every main SCS aggregate.
- Use account user IDs only through explicit references; do not duplicate account user auth state in main.
- Store provider credentials encrypted and never expose raw token values to frontend.
- Use optimistic or transactional reservation semantics for slot holds.
- Store provider webhook IDs, sync cursors, Flow IDs, and published Flow versions separately from tenant display settings.
- Make idempotency keys first-class for WhatsApp callbacks, provider webhooks, booking creation, and outbound side effects.


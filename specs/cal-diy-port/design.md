# Cal.diy Port Design

## Overview

Port Cal.diy into NerovaBookings as a source-faithful Solo scheduling tier. The target is a native Nerova implementation: .NET backend, Postgres persistence, React/Rsbuild frontend, generated OpenAPI clients, TanStack Query, Lingui, and `@repo/ui`.

Cal.diy is the behavioral source of truth. The intentional deviations are Nerova architecture, Solo-tier gating, and replacement of public web booking with WhatsApp Business Flow booking.

## Locked Product Decisions

- Tier: Solo businesses only for v1.
- Public booking: WhatsApp Flow only. Cal.diy public booking and embed pages are behavior references, not public web UI deliverables.
- Visible app-store connectors: `googlecalendar`, `googlevideo`, `office365calendar`, `office365video` with slug `msteams`, `zoomvideo`, and native `whatsapp`.
- WhatsApp: Cal.diy static `wa.me` location behavior is folded into a native WhatsApp Business Flow connector. It is not a separate app tile.
- Out of v1: Gmail, Drive, OneDrive, SharePoint, Outlook Mail, Teams chat, CRM, analytics, payments, and other app-store integrations except shared app-store infrastructure patterns.
- Styling: `@repo/ui` primitives stay untouched unless separately approved. Scheduling-specific adapter components may be added in `application/main/WebApp`.

## Source Snapshot

- Cal.diy path: `cal.diy`
- Branch: `main`
- Commit: `180ede28f0bddf2738933a6e60a8e80f6116d7da`
- Status: `## main...origin/main`
- Package manager: `yarn@4.12.0`
- `rg --files` count: 7256
- Direct inventory count including additional support files: 7686
- Test/spec/e2e related files from `rg`: 526
- Test/spec/e2e related files in direct inventory: 534

## Target Architecture

Backend domains live in `application/main`: scheduling, event types, slots, bookings, connectors, app-store registry, credentials, tasks, notifications, webhooks, audit, and Solo operational reporting.

Account plan and tenant eligibility changes live in `application/account`: add Solo plan/capability data and expose it through backend/frontend guards.

Frontend lives in `application/main/WebApp`: port authenticated Cal.diy admin layouts and flows using Nerova shell, generated OpenAPI clients, TanStack Query, Lingui, and `@repo/ui`. Public booking flows are implemented through WhatsApp Flow callbacks, not public web pages.

## Data Model Anchors

| Model | Line | Target |
| --- | --- | --- |
| Host | 61 | EF Core entity/value object mapping review required |
| HostGroup | 87 | EF Core entity/value object mapping review required |
| HostLocation | 100 | EF Core entity/value object mapping review required |
| CalVideoSettings | 119 | EF Core entity/value object mapping review required |
| VideoCallGuest | 135 | EF Core entity/value object mapping review required |
| EventType | 156 | EF Core entity/value object mapping review required |
| Credential | 308 | EF Core entity/value object mapping review required |
| DestinationCalendar | 351 | EF Core entity/value object mapping review required |
| UserPassword | 381 | EF Core entity/value object mapping review required |
| TravelSchedule | 387 | EF Core entity/value object mapping review required |
| User | 401 | EF Core entity/value object mapping review required |
| NotificationsSubscriptions | 524 | EF Core entity/value object mapping review required |
| Profile | 534 | EF Core entity/value object mapping review required |
| Team | 557 | EF Core entity/value object mapping review required |
| CreditBalance | 652 | EF Core entity/value object mapping review required |
| CreditPurchaseLog | 666 | EF Core entity/value object mapping review required |
| CreditExpenseLog | 679 | EF Core entity/value object mapping review required |
| OrganizationSettings | 704 | EF Core entity/value object mapping review required |
| Membership | 744 | EF Core entity/value object mapping review required |
| VerificationToken | 767 | EF Core entity/value object mapping review required |
| InstantMeetingToken | 786 | EF Core entity/value object mapping review required |
| BookingReference | 801 | EF Core entity/value object mapping review required |
| Attendee | 826 | EF Core entity/value object mapping review required |
| Booking | 851 | EF Core entity/value object mapping review required |
| Tracking | 932 | EF Core entity/value object mapping review required |
| Schedule | 945 | EF Core entity/value object mapping review required |
| Availability | 960 | EF Core entity/value object mapping review required |
| SelectedCalendar | 978 | EF Core entity/value object mapping review required |
| EventTypeCustomInput | 1058 | EF Core entity/value object mapping review required |
| ResetPasswordRequest | 1072 | EF Core entity/value object mapping review required |
| ReminderMail | 1084 | EF Core entity/value object mapping review required |
| Payment | 1095 | EF Core entity/value object mapping review required |
| Webhook | 1142 | EF Core entity/value object mapping review required |
| ApiKey | 1172 | EF Core entity/value object mapping review required |
| RateLimit | 1190 | EF Core entity/value object mapping review required |
| HashedLink | 1205 | EF Core entity/value object mapping review required |
| Account | 1217 | EF Core entity/value object mapping review required |
| Session | 1239 | EF Core entity/value object mapping review required |
| App | 1263 | EF Core entity/value object mapping review required |
| Feedback | 1283 | EF Core entity/value object mapping review required |
| Deployment | 1295 | EF Core entity/value object mapping review required |
| WebhookScheduledTriggers | 1313 | EF Core entity/value object mapping review required |
| BookingSeat | 1330 | EF Core entity/value object mapping review required |
| VerifiedNumber | 1345 | EF Core entity/value object mapping review required |
| VerifiedEmail | 1357 | EF Core entity/value object mapping review required |
| Feature | 1369 | EF Core entity/value object mapping review required |
| UserFeatures | 1391 | EF Core entity/value object mapping review required |
| TeamFeatures | 1405 | EF Core entity/value object mapping review required |
| SelectedSlots | 1437 | EF Core entity/value object mapping review required |
| OAuthClient | 1461 | EF Core entity/value object mapping review required |
| AccessCode | 1481 | EF Core entity/value object mapping review required |
| BookingDenormalized | 1527 | EF Core entity/value object mapping review required |
| CalendarCache | 1594 | EF Core entity/value object mapping review required |
| TempOrgRedirect | 1620 | EF Core entity/value object mapping review required |
| Avatar | 1636 | EF Core entity/value object mapping review required |
| OutOfOfficeEntry | 1652 | EF Core entity/value object mapping review required |
| OutOfOfficeReason | 1675 | EF Core entity/value object mapping review required |
| UserHolidaySettings | 1686 | EF Core entity/value object mapping review required |
| HolidayCache | 1698 | EF Core entity/value object mapping review required |
| PlatformOAuthClient | 1716 | EF Core entity/value object mapping review required |
| PlatformAuthorizationToken | 1743 | EF Core entity/value object mapping review required |
| AccessToken | 1757 | EF Core entity/value object mapping review required |
| RefreshToken | 1771 | EF Core entity/value object mapping review required |
| DSyncData | 1785 | EF Core entity/value object mapping review required |
| DSyncTeamGroupMapping | 1797 | EF Core entity/value object mapping review required |
| SecondaryEmail | 1809 | EF Core entity/value object mapping review required |
| Task | 1825 | EF Core entity/value object mapping review required |
| ManagedOrganization | 1859 | EF Core entity/value object mapping review required |
| PlatformBilling | 1872 | EF Core entity/value object mapping review required |
| AttributeOption | 1900 | EF Core entity/value object mapping review required |
| Attribute | 1915 | EF Core entity/value object mapping review required |
| AttributeToUser | 1943 | EF Core entity/value object mapping review required |
| AssignmentReason | 1980 | EF Core entity/value object mapping review required |
| DelegationCredential | 1996 | EF Core entity/value object mapping review required |
| WorkspacePlatform | 2025 | EF Core entity/value object mapping review required |
| EventTypeTranslation | 2042 | EF Core entity/value object mapping review required |
| Watchlist | 2079 | EF Core entity/value object mapping review required |
| WatchlistAudit | 2098 | EF Core entity/value object mapping review required |
| WatchlistEventAudit | 2115 | EF Core entity/value object mapping review required |
| BookingReport | 2141 | EF Core entity/value object mapping review required |
| WrongAssignmentReport | 2180 | EF Core entity/value object mapping review required |
| OrganizationOnboarding | 2215 | EF Core entity/value object mapping review required |
| InternalNotePreset | 2271 | EF Core entity/value object mapping review required |
| FilterSegment | 2290 | EF Core entity/value object mapping review required |
| UserFilterSegmentPreference | 2319 | EF Core entity/value object mapping review required |
| BookingInternalNote | 2335 | EF Core entity/value object mapping review required |
| Role | 2359 | EF Core entity/value object mapping review required |
| RolePermission | 2376 | EF Core entity/value object mapping review required |
| AuditActor | 2447 | EF Core entity/value object mapping review required |
| BookingAudit | 2485 | EF Core entity/value object mapping review required |
| Agent | 2541 | EF Core entity/value object mapping review required |
| CalAiPhoneNumber | 2573 | EF Core entity/value object mapping review required |
| TeamBilling | 2607 | EF Core entity/value object mapping review required |
| OrganizationBilling | 2636 | EF Core entity/value object mapping review required |
| SeatChangeLog | 2665 | EF Core entity/value object mapping review required |
| MonthlyProration | 2703 | EF Core entity/value object mapping review required |
| CalendarCacheEvent | 2770 | EF Core entity/value object mapping review required |
| IntegrationAttributeSync | 2802 | EF Core entity/value object mapping review required |
| AttributeSyncRule | 2824 | EF Core entity/value object mapping review required |
| AttributeSyncFieldMapping | 2836 | EF Core entity/value object mapping review required |

## Public API Surface To Plan

- Event type CRUD and state transitions.
- Availability schedule CRUD, date overrides, timezone handling, default schedule resolution.
- Slot query/reservation/get/update/delete using Cal.diy `slots-2024-09-04` behavior as primary source.
- Booking lifecycle: create, get, list, confirm/reject, reschedule, cancel, no-show, audit.
- Connector registry/install/uninstall/OAuth callback/credential state.
- Selected calendars, destination calendars, conferencing providers.
- WhatsApp webhook verification, Flow data exchange, callback idempotency, and booking action endpoints.
- Webhook subscription/delivery and task status where needed for app behavior.

## First Implementation Wave

1. Source inventory and upstream diff gate.
2. Solo plan and frontend/backend guards.
3. App-store registry foundation with connector metadata and credential state.
4. Event type and availability data model foundation.
5. Slot algorithm port with translated Cal.diy tests.
6. Booking lifecycle core.
7. Workforce connectors.
8. WhatsApp Flow booking.
9. Admin UI and atom parity.
10. Background tasks, notifications, webhook delivery, audit/reporting.
11. Full E2E and visual parity hardening.

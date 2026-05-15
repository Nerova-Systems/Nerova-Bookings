# Source Inventory

This document classifies Cal.diy's source tree for the Nerova port. It does not prescribe file-by-file copying. It defines which source areas are behavioral or layout sources, which are infrastructure references, and which areas are deferred or rejected for Solo v1.

## Top-Level Apps

| Source | Role | Status | Nerova use |
| --- | --- | --- | --- |
| `cal.diy/apps/api` | API proxy plus `v2` Nest API. | Port/Replace | Treat `v2` as endpoint and DTO behavior reference. Rebuild in .NET Minimal APIs/CQRS/OpenAPI. |
| `cal.diy/apps/web` | Main Next web application. | Port/Replace | Treat as UI/layout/workflow source. Rebuild in React/Rsbuild using Nerova routes and `@repo/ui`. |
| `cal.diy/apps/docs` | Nextra docs. | Reference | Use app and deployment docs as implementation intent, not as runtime code. |

Additional app support directories:

- `apps/web/styles`: reference/replace; use only for layout and state clues, not as styling runtime.
- `apps/web/test-results`: reject as generated artifact; do not port.

## API v2 Modules

`apps/api/v2/src/modules/endpoints.module.ts` wires OAuth2, OAuth clients, timezones, users, webhooks, destination calendars, atoms, Stripe, conferencing, unified calendars, and verified resources.

`apps/api/v2/src/platform/platform-endpoints-module.ts` wires GCal, provider, schedules, me, event types, calendars, bookings, slots, and private links with versioned modules.

| Module | Files | Tests | Status | Nerova target |
| --- | ---: | ---: | --- | --- |
| `api-keys` | 8 | 0 | Defer | Do not expose Cal.diy API key management in Solo v1 unless Nerova API access is explicitly scoped later. |
| `apps` | 3 | 0 | Port | App registry lookup and Google Calendar service behavior become integration registry services. |
| `atoms` | 22 | 0 | Port/Replace | Atom API behavior becomes generated OpenAPI endpoints used by Nerova React. |
| `auth` | 43 | 5 | Replace | Use Nerova account auth/session model; port only OAuth client and permission ideas needed by connectors. |
| `booking-seat` | 2 | 0 | Defer | Seated events are out of Solo v1. Keep model room for later. |
| `cal-unified-calendars` | 21 | 6 | Port | Unified calendar free/busy and event actions map to connector services. |
| `conferencing` | 12 | 1 | Port | Google Meet, Teams, Zoom, and default conferencing behavior. |
| `credentials` | 2 | 0 | Replace | Use Nerova encrypted integration credentials. |
| `deployments` | 3 | 0 | Defer | Cal deployment management is not part of Nerova Solo scheduling. |
| `destination-calendars` | 7 | 1 | Port | Destination calendar write target. |
| `email` | 2 | 0 | Replace | Use Nerova email infrastructure; preserve trigger semantics. |
| `event-types` | 4 | 1 | Port | Event-type webhooks and access checks. Core event-type APIs live under platform modules. |
| `jwt` | 2 | 0 | Replace | Use Nerova token/session conventions. |
| `kysely` | 3 | 0 | Reject | Do not bring Kysely runtime. Use EF Core/Postgres. |
| `memberships` | 3 | 0 | Reference | Solo v1 maps to Nerova tenant membership; team scheduling is deferred. |
| `oauth-clients` | 30 | 3 | Defer | Platform OAuth clients are not a Solo v1 customer surface. Keep as future API platform reference. |
| `ooo` | 5 | 0 | Port | Out-of-office rules affect availability and slots. |
| `organizations` | 2 | 0 | Reject | Cal organization model does not become Nerova tenancy. |
| `prisma` | 4 | 0 | Reject | No Prisma runtime. Use schema only as data-model reference. |
| `profiles` | 2 | 0 | Port | Provider/staff profile metadata needed for event types and WhatsApp. |
| `redis` | 2 | 0 | Replace | Use Nerova cache/job infrastructure; do not depend on Cal Redis shape. |
| `selected-calendars` | 7 | 1 | Port | Selected calendars are conflict sources. |
| `slots` | 23 | 6 | Port | Slot calculation, reservation, and stale-slot handling are core. |
| `stripe` | 8 | 0 | Defer | Cal payments are out of Solo v1; Nerova billing remains Paystack/account-owned. |
| `teams` | 6 | 0 | Reference | Future multi-staff/team scheduling only. |
| `timezones` | 3 | 0 | Port | Time zone list and validation behavior. |
| `tokens` | 3 | 0 | Replace | Use Nerova token storage patterns. |
| `users` | 17 | 1 | Replace | Map to Nerova account users and provider profiles. |
| `verified-resources` | 13 | 0 | Port | Email/phone verification supports WhatsApp and booking identity. |
| `webhooks` | 23 | 2 | Port | External booking lifecycle webhooks and validation behavior. |

## API Platform Modules

| Platform source | Status | Nerova target |
| --- | --- | --- |
| `platform/bookings/2024-04-15` | Reference | Older API version for compatibility behavior only. |
| `platform/bookings/2024-08-13` | Port | Booking create, reschedule, cancel, attendees, guests, location, confirm/reject. |
| `platform/calendars` | Port | Calendar connection, busy time, selected/destination calendar behavior. |
| `platform/event-types/event-types_2024_04_15` | Reference | Version-diff source. |
| `platform/event-types/event-types_2024_06_14` | Port | Event type API and DTO behavior. |
| `platform/event-types-private-links` | Port | Private booking links if WhatsApp flow needs private invite links and future web fallbacks. |
| `platform/gcal` | Port | Google Calendar OAuth and callback behavior. |
| `platform/me` | Replace | Use Nerova user info endpoint and runtime user meta. |
| `platform/provider` | Reference | Provider concept maps to Solo staff profile. |
| `platform/schedules/schedules_2024_04_15` | Reference | Version-diff source. |
| `platform/schedules/schedules_2024_06_11` | Port | Schedule and availability API behavior. |

## Web App Source

| Source | Files | Tests | Status | Nerova target |
| --- | ---: | ---: | --- | --- |
| `apps/web/app` | 211 | Included separately | Port/Replace | Route tree and page-loader behavior. Public booking pages are replaced by WhatsApp for Solo. |
| `apps/web/modules/api-keys` | 2 | 0 | Defer | API key UI out of v1. |
| `apps/web/modules/apps` | 23 | 1 | Port | App-store catalog, installed apps, connector setup layouts. |
| `apps/web/modules/auth` | 14 | 0 | Replace | Use Nerova account auth UI. |
| `apps/web/modules/availability` | 3 | 0 | Port | Availability dashboard. |
| `apps/web/modules/blocklist` | 9 | 0 | Defer | Watchlist/blocking later unless WhatsApp abuse controls require it. |
| `apps/web/modules/booking-audit` | 2 | 0 | Port | Booking audit views once booking lifecycle exists. |
| `apps/web/modules/bookings` | 105 | 5 | Port/Replace | Admin booking views port; public Booker state logic maps to WhatsApp flow callbacks. |
| `apps/web/modules/calendar-view` | 1 | 0 | Defer | Calendar UI is secondary to booking/admin flows. |
| `apps/web/modules/calendars` | 19 | 0 | Port | Calendar settings and connected calendars. |
| `apps/web/modules/connect-and-join` | 1 | 0 | Defer | Not needed for Solo v1. |
| `apps/web/modules/d` | 1 | 0 | Port | Private link route reference only. |
| `apps/web/modules/data-table` | 49 | 1 | Port/Replace | Recreate table/filter UX with `@repo/ui` tables. |
| `apps/web/modules/embed` | 2 | 0 | Reference | Embeds deferred; keep atom/SDK semantics documented. |
| `apps/web/modules/event-types` | 39 | 1 | Port | Event type list/editor/tabs. |
| `apps/web/modules/feature-flags` | 4 | 0 | Reference | Use Nerova feature gates and Solo plan. |
| `apps/web/modules/filters` | 2 | 0 | Port | Booking/admin filter behavior. |
| `apps/web/modules/form-builder` | 3 | 1 | Port | Custom booking questions and WhatsApp Flow fields. |
| `apps/web/modules/formbricks` | 2 | 0 | Defer | External survey integration out of v1. |
| `apps/web/modules/getting-started` | 1 | 0 | Port/Replace | Solo onboarding flow. |
| `apps/web/modules/maintenance` | 1 | 0 | Defer | Not a scheduling capability. |
| `apps/web/modules/more` | 1 | 0 | Reference | Navigation grouping only. |
| `apps/web/modules/notifications` | 2 | 0 | Port/Replace | Notification preferences and triggers via Nerova infrastructure. |
| `apps/web/modules/onboarding` | 28 | 1 | Port/Replace | Solo setup flow. |
| `apps/web/modules/schedules` | 9 | 1 | Port | Schedule editor/date overrides. |
| `apps/web/modules/settings` | 35 | 0 | Port/Replace | Account settings relevant to scheduling and connectors. |
| `apps/web/modules/shell` | 19 | 1 | Port/Replace | Layout/navigation patterns adapted to Nerova app shell. |
| `apps/web/modules/timezone` | 2 | 1 | Port | Timezone picker/format behavior. |
| `apps/web/modules/troubleshooter` | 9 | 0 | Port | Availability/calendar troubleshooting. |
| `apps/web/modules/upgrade` | 1 | 0 | Replace | Use Nerova subscription UI and Solo plan. |
| `apps/web/modules/users` | 32 | 3 | Replace/Reference | User profile behavior maps to account users/provider profiles. |
| `apps/web/modules/videos` | 7 | 1 | Port if conferencing requires | Video pages follow connector scope. |
| `apps/web/modules/webhooks` | 15 | 1 | Port | Developer webhook UI after booking webhooks exist. |

Additional web support source:

- `apps/web/styles`: reference only for existing layout/theme behavior. Nerova styling remains `@repo/ui` and local Tailwind conventions.
- `apps/web/test-results`: generated Playwright output, not a source input.

## Web Route Families

| Route family | Status | Nerova treatment |
| --- | --- | --- |
| `(booking-page-wrapper)` public profile, event, embed, private link, success, cancel, booking detail | Replace | Do not expose Solo public web booking. Port state machine into WhatsApp Flow data exchange. Keep success/cancel/detail behavior for admin and future web fallback. |
| `(use-page-wrapper)/(main-nav)/event-types` | Port | Solo service/event type dashboard. |
| `(use-page-wrapper)/event-types/[type]` | Port | Event type editor. |
| `(use-page-wrapper)/(main-nav)/availability` and `availability/[schedule]` | Port | Availability/schedule list and editor. |
| `(use-page-wrapper)/(main-nav)/bookings/[status]` | Port | Booking dashboard by lifecycle status. |
| `(use-page-wrapper)/apps/*` | Port | App-store homepage, category, installation, installed, setup pages limited to v1 connector catalog. |
| `(use-page-wrapper)/settings/(settings-layout)/my-account/calendars` | Port | Calendar connection settings. |
| `(use-page-wrapper)/settings/(settings-layout)/my-account/conferencing` | Port | Conferencing provider settings for Google Meet, Teams, Zoom. |
| `(use-page-wrapper)/settings/(settings-layout)/developer/webhooks` | Port later | Webhooks after booking lifecycle events exist. |
| Auth, signup, password, two-factor routes | Replace | Existing Nerova account SCS owns auth. |
| Admin settings, flags, users, oauth admin | Reference/Defer | Not Solo scheduling v1. |
| Video routes | Port if provider scope requires | Use only for configured conferencing providers. |
| Payment routes | Defer | Cal payments out of v1. |

## Docs Source

| Docs source | Status | Nerova use |
| --- | --- | --- |
| `apps/docs/content/index.mdx` | Reference | Product/documentation intent and source feature matrix. |
| `apps/docs/content/installation.mdx` | Reference | Setup assumptions only. |
| `apps/docs/content/upgrading.mdx` | Reference | Upgrade/release clues only. |
| `apps/docs/content/troubleshooting.mdx` | Reference | Operational troubleshooting patterns. |
| `apps/docs/content/docker.mdx` | Reference | Deployment assumptions only; Nerova runtime is not Docker-compose driven by Cal.diy. |
| `apps/docs/content/database-migrations.mdx` | Reference | Migration intent only; Nerova uses EF Core migrations. |
| `apps/docs/content/_meta.ts` | Reference | Docs navigation only. |
| `apps/docs/content/apps/_meta.ts` | Reference | Docs navigation only. |
| `apps/docs/content/apps/google.mdx` | Reference | Google Calendar and Meet setup intent. |
| `apps/docs/content/apps/microsoft.mdx` | Reference | Outlook Calendar and Teams setup intent. |
| `apps/docs/content/apps/zoom.mdx` | Reference | Zoom setup intent. |
| `apps/docs/content/apps/daily.mdx` | Reference/Defer | Daily/Cal Video pattern if fallback video is required. |
| `apps/docs/content/apps/hubspot.mdx` | Defer | CRM out of Solo v1. |
| `apps/docs/content/apps/sendgrid.mdx` | Defer/Reference | Email provider reference only. |
| `apps/docs/content/apps/stripe.mdx` | Defer | Cal payment behavior out of Solo v1. |
| `apps/docs/content/apps/twilio.mdx` | Reference/Defer | Messaging provider pattern only; WhatsApp connector is native Meta. |
| `apps/docs/content/apps/zoho.mdx` | Defer | Zoho apps out of Solo v1. |
| `apps/docs/content/deployments/_meta.ts` | Reference | Docs navigation only. |
| `apps/docs/content/deployments/azure.mdx` | Reference | Deployment assumptions only; Nerova remains Aspire/Azure-oriented. |
| `apps/docs/content/deployments/aws.mdx` | Reference | Deployment comparison only. |
| `apps/docs/content/deployments/gcp.mdx` | Reference | Deployment comparison only. |
| `apps/docs/content/deployments/vercel.mdx` | Reference | Deployment comparison only. |
| `apps/docs/content/deployments/render.mdx` | Reference | Deployment comparison only. |
| `apps/docs/content/deployments/railway.mdx` | Reference | Deployment comparison only. |
| `apps/docs/content/deployments/northflank.mdx` | Reference | Deployment comparison only. |
| `apps/docs/content/deployments/elestio.mdx` | Reference | Deployment comparison only. |

## Packages

| Package | Files | Tests | Status | Nerova target |
| --- | ---: | ---: | --- | --- |
| `app-store` | 1,529 | 37 | Port/Filter | Port registry, metadata, install/credential/dependency patterns. Filter visible catalog to v1 connectors. |
| `app-store-cli` | 27 | 1 | Port/Replace | Recreate as developer CLI integration generator/validator. |
| `config` | 5 | 0 | Reference | Configuration conventions only. |
| `coss-ui` | 61 | 0 | Port/Map | Atom-level component source; map to `@repo/ui`. |
| `dayjs` | 6 | 0 | Port/Replace | Preserve date/time behavior using .NET/React date utilities. |
| `debugging` | 4 | 0 | Reference | Logging/debug behavior only. |
| `emails` | 150 | 6 | Port/Replace | Preserve templates/triggers, render/send via Nerova email infrastructure. |
| `embeds` | 102 | 24 | Reference/Defer | Atom/SDK/embed semantics documented; public embeds deferred. |
| `features` | 987 | 206 | Port/Filter | Main business-rule source. Defer out-of-scope feature dirs explicitly. |
| `i18n` | 50 | 2 | Replace | Use Nerova Lingui, preserve needed strings/locale behavior. |
| `kysely` | 4 | 0 | Reject | No Kysely runtime. |
| `lib` | 290 | 47 | Port/Replace | Utilities, scheduling helpers, security helpers, tasker concepts. Rebuild per Nerova rules. |
| `platform` | 407 | 11 | Port/Replace | API types/libraries/atoms. Convert to .NET DTOs/OpenAPI and React components. |
| `prisma` | 625 | 2 | Reference | Schema and migrations are data-model source only. No Prisma runtime. |
| `sms` | 11 | 1 | Port/Replace | Notification semantics, especially WhatsApp/SMS-style reminders. |
| `testing` | 29 | 29 | Reference | Test fixtures/scenarios to recreate in .NET/Playwright. |
| `trpc` | 431 | 32 | Port/Replace | Dashboard behavior oracle. Convert to .NET endpoints and generated client usage. |
| `tsconfig` | 5 | 0 | Reject | Not relevant to Nerova runtime. |
| `types` | 32 | 0 | Port/Replace | Convert app/event/booking types into C# DTOs and TypeScript generated models. |
| `ui` | 226 | 34 | Port/Map | UI behavior/layout reference, mapped to `@repo/ui`. |

## Feature Package Classification

Port: `apps`, `availability`, `booking-audit`, `bookingReference`, `bookingReport`, `bookings`, `busyTimes`, `calendar-subscription`, `calendars`, `calVideoSettings`, `cityTimezones`, `components`, `conferencing`, `credentials`, `eventtypes`, `form`, `form-builder`, `hashedLink`, `holidays`, `links`, `notifications`, `ooo`, `profile`, `schedules`, `selectedCalendar`, `selectedSlots`, `settings`, `slots`, `tasker`, `timezone`, `translation`, `travelSchedule`, `troubleshooter`, `users`, `video-call-guest`, `webhooks`.

Replace: `auth`, `cache`, `deployment`, `di`, `embed`, `feature-opt-in`, `flags`, `i18n-related translation surfaces`, `oauth`, `onboarding`, `platform-oauth-client`, `redis`.

Reference/Defer: `api-keys-legacy`, `assignment-reason`, `blocklist`, `bot-detection`, `credits`, `crmManager`, `data-table`, `eventTypeTranslation`, `filters`, `host`, `membership`, `noShow`, `url-shortener`, `watchlist`.

Defer/Reject for Solo v1: team/organization-specific behavior, CRM-specific behavior, payment-specific behavior, AI-agent behavior, commercial Cal.com enterprise remnants, and any feature that depends on public web booking as the customer surface.

## App-Store Connector Classification

Infrastructure to port: `_components`, `_lib`, `_pages`, `_utils`, `templates`, `tests`.

Visible Solo v1 app tiles to port: `googlecalendar`, `googlevideo`, `office365calendar`, `office365video`, `zoomvideo`, `whatsapp`.

Connector references, not visible v1 tiles: `dailyvideo` as default-video adapter reference if conferencing fallback is required; `ics-feedcalendar` as future calendar reference; `caldavcalendar`, `applecalendar`, `exchange2013calendar`, `exchange2016calendar`, `exchangecalendar`, `larkcalendar`, `feishucalendar`, `zohocalendar` as future calendar provider references.

Deferred app-store entries: `alby`, `amie`, `attio`, `autocheckin`, `baa-for-hipaa`, `basecamp3`, `bolna`, `btcpayserver`, `campfire`, `caretta`, `chatbase`, `clic`, `closecom`, `cron`, `databuddy`, `deel`, `demodesk`, `dialpad`, `discord`, `dub`, `eightxeight`, `element-call`, `elevenlabs`, `facetime`, `famulor`, `fathom`, `fonio-ai`, `framer`, `ga4`, `giphy`, `granola`, `greetmate-ai`, `gtm`, `hitpay`, `horizon-workrooms`, `huddle01video`, `insihts`, `intercom`, `jelly`, `jitsivideo`, `lindy`, `linear`, `lyra`, `make`, `matomo`, `metapixel`, `millis-ai`, `mirotalk`, `mock-payment-app`, `monobot`, `n8n`, `nextcloudtalk`, `paypal`, `ping`, `pipedream`, `pipedrive-crm`, `plausible`, `posthog`, `qr_code`, `raycast`, `retell-ai`, `riverside`, `roam`, `salesforce`, `salesroom`, `sendgrid`, `shimmervideo`, `signal`, `sirius_video`, `skype`, `stripepayment`, `sylapsvideo`, `synthflow`, `tandemvideo`, `telegram`, `telli`, `twipla`, `umami`, `vimcal`, `vital`, `weather_in_your_calendar`, `webex`, `whereby`, `wipemycalother`, `wordpress`, `zapier`, `zoho-bigin`, `zohocrm`.

Rejected for Solo v1 runtime: any connector that requires Cal.com marketplace breadth, CRM ownership, analytics ownership, payment ownership, AI agent ownership, or a public web booking surface.

## Platform Library Exhaustive Coverage

The `packages/platform/libraries` files are classified by exact source name:

- Port/replace for included behavior: `app-store`, `bookings`, `calendars`, `conferencing`, `emails`, `errors`, `event-types`, `private-links`, `schedules`, `slots`, `tasker`.
- Reference or replace infrastructure: `index`, `organizations`, `repositories`. `repositories` is a repository abstraction reference only; use Nerova repositories and EF Core patterns.

## Web Route Directory Exhaustive Coverage

Route syntax/support directories are classified here so implementation scans do not miss source areas that are not product features by themselves:

- Route framework and grouping: `_trpc`, `(admin-layout)`, `(homepage)`, `(with-loader)`, `[[...step]]`, `[category]`, `[id]`, `[link]`, `[provider]`, `[slug]`, `[uid]`, `[user]`, `[uuid]`.
- Public booking and replacement-reference routes: `booking-successful`, `dry-run-successful`, `verify-booking-token`.
- Auth/session routes to replace with Nerova account SCS: `csrf`, `forgot-password`, `guest-session`, `oauth2`, `refreshToken`, `reset-password`, `session-warmup`, `signin`, `totp`, `two-factor-auth`, `verify-email`, `verify-email-change`.
- Scheduling, calendar, notification, and app-store API support to port or replace as part of included domains: `app-credential`, `bookingReminder`, `calendar-subscriptions`, `calendar-subscriptions-cleanup`, `changeTimeZone`, `geolocation`, `syncAppMeta`, `username`, `webhookTriggers`.
- Admin, settings, video, and support surfaces to reference/defer unless needed by included connectors: `date-range-filter`, `helpscout`, `icons`, `lockedSMS`, `meeting-ended`, `meeting-not-started`, `no-meeting-found`, `personal`, `playground`, `push-notifications`, `recorded-daily-video`, `recording`, `referrals-token`, `social`.

## tRPC Viewer Router Exhaustive Coverage

The dashboard router directory names are classified by exact source name:

- Port/replace for included domains: `apps`, `availability`, `bookings`, `calendars`, `calVideo`, `credentials`, `eventTypes`, `googleWorkspace`, `holidays`, `i18n`, `me`, `ooo`, `slots`, `travelSchedules`, `users`, `webhook`.
- Defer or replace as platform/account concerns: `admin`, `apiKeys`, `auth`, `deploymentSetup`, `feedback`, `filterSegments`, `oAuth`, `payments`.

## Prisma Exhaustive Name Coverage

The Prisma schema is not a runtime dependency, but every model and enum name is a data-dictionary input. These names are classified by target intent:

- Port or filter into scheduling domains: `Availability`, `Booking`, `BookingAudit`, `BookingAuditAction`, `BookingAuditSource`, `BookingAuditType`, `BookingDenormalized`, `BookingInternalNote`, `BookingReference`, `BookingReport`, `BookingReportReason`, `BookingReportStatus`, `BookingSeat`, `BookingStatus`, `CalVideoSettings`, `CancellationReasonRequirement`, `CreationSource`, `DestinationCalendar`, `EventType`, `EventTypeAutoTranslatedField`, `EventTypeCustomInput`, `EventTypeCustomInputType`, `EventTypeTranslation`, `HashedLink`, `Host`, `HostGroup`, `HostLocation`, `NotificationsSubscriptions`, `OutOfOfficeEntry`, `OutOfOfficeReason`, `PeriodType`, `Profile`, `ReminderMail`, `ReminderType`, `Schedule`, `SchedulingType`, `SelectedCalendar`, `SelectedSlots`, `SystemReportStatus`, `Task`, `TimeUnit`, `Tracking`, `TravelSchedule`, `UserHolidaySettings`, `VideoCallGuest`, `Webhook`, `WebhookScheduledTriggers`, `WebhookTriggerEvents`, `WrongAssignmentReport`, `WrongAssignmentReportStatus`.
- Replace with Nerova account/auth/integration architecture: `AccessCode`, `AccessScope`, `AccessToken`, `Account`, `ApiKey`, `App`, `AppCategories`, `AuditActor`, `AuditActorType`, `Avatar`, `Credential`, `DelegationCredential`, `Deployment`, `Feature`, `FeatureType`, `FilterSegment`, `FilterSegmentScope`, `IdentityProvider`, `InstantMeetingToken`, `OAuthClient`, `OAuthClientStatus`, `OAuthClientType`, `PlatformAuthorizationToken`, `PlatformOAuthClient`, `RateLimit`, `RedirectType`, `RefreshToken`, `ResetPasswordRequest`, `Role`, `RolePermission`, `RoleType`, `SecondaryEmail`, `Session`, `User`, `UserFeatures`, `UserFilterSegmentPreference`, `UserPassword`, `UserPermissionRole`, `VerificationToken`, `VerifiedEmail`, `VerifiedNumber`, `WorkspacePlatform`.
- Reference or defer team, organization, routing, and enterprise remnants: `AssignmentReason`, `AssignmentReasonEnum`, `Attribute`, `AttributeOption`, `AttributeSyncFieldMapping`, `AttributeSyncRule`, `AttributeToUser`, `AttributeType`, `DSyncData`, `DSyncTeamGroupMapping`, `ManagedOrganization`, `Membership`, `MembershipRole`, `OrganizationOnboarding`, `OrganizationSettings`, `RRResetInterval`, `RRTimestampBasis`, `Team`, `TeamFeatures`, `TempOrgRedirect`.
- Defer or reject billing, AI, watchlist, and non-v1 operational models: `Agent`, `BillingMode`, `BillingPeriod`, `CalAiPhoneNumber`, `CalendarCache`, `CalendarCacheEvent`, `CalendarCacheEventStatus`, `CreditBalance`, `CreditExpenseLog`, `CreditPurchaseLog`, `CreditType`, `CreditUsageType`, `Feedback`, `HolidayCache`, `IntegrationAttributeSync`, `InternalNotePreset`, `MonthlyProration`, `OrganizationBilling`, `Payment`, `PaymentOption`, `PhoneNumberSubscriptionStatus`, `PlatformBilling`, `ProrationStatus`, `SeatChangeLog`, `SeatChangeType`, `SMSLockState`, `TeamBilling`, `Watchlist`, `WatchlistAction`, `WatchlistAudit`, `WatchlistEventAudit`, `WatchlistSource`, `WatchlistType`.

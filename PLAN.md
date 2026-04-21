# BookSlot — Master Product & Technical Plan

> Transfer this file to the root of the new codebase. It is the authoritative reference for what we are building, why, and how.

---

## 1. Product Vision

**BookSlot** is a SaaS appointment-booking platform built for non-technical professional business owners in South Africa (primary), UK, US, and Australia.

### Core Philosophy: "Buy and Use"

> *"The entire idea of the app is providing a ready-built system for non-technical professional business owners. No one wants to invest time to configure a random flow. It's buy, and use. That is it."*

This means:
- **Zero manual configuration** for any feature. Everything is pre-built and activated via simple toggles.
- **No flow builders**, no drag-and-drop tools, no custom template editors.
- Every integration (WhatsApp, calendar, payments) must work out-of-the-box the moment a tenant enables it.
- The UI is designed for confidence, not complexity.

### Target Markets
- **Primary:** South Africa (ZA) — POPIA compliance required
- **Secondary:** UK, US, Australia

### Compliance Notes
- **POPIA (ZA):** Opt-in for WhatsApp must capture purpose + timestamp + consent source
- **GDPR (UK/EU):** Same requirements where applicable
- **No live tenants yet** — dev/staging only. Migrations can be drop-and-rebuild without backfill.

---

## 2. Repository Base

Clone [PlatformPlatform](https://github.com/platformplatform/PlatformPlatform) and rename throughout.

**What the boilerplate provides (do not rebuild):**
- Multi-tenant account SCS (`application/account/`) — tenant creation, user management, login/signup
- Back-office SCS (`application/back-office/`) — support admin, tenant impersonation
- Shared kernel (`application/shared-kernel/`) — domain primitives, EF Core infrastructure, pipeline behaviors
- Shared webapp (`application/shared-webapp/`) — React UI components, API client, auth hooks
- AppHost (`application/AppHost/`) — .NET Aspire orchestration for all SCSs + Docker containers
- AppGateway (`application/AppGateway/`) — YARP reverse proxy routing all SCSs under one host
- Azure Bicep IaC (`cloud-infrastructure/`)
- Developer CLI (`developer-cli/`) — build, test, format, lint commands
- GitHub Actions CI/CD (`.github/`)
- Agent rules (`.claude/`)

**Rename checklist (PlatformPlatform → BookSlot):**
- Solution file names (`.slnx`, `.slnf`)
- Root namespaces in all `Directory.Build.props`
- `AppHost` project name registrations
- `AppGateway` YARP route prefixes
- Azure Bicep resource names
- GitHub Actions workflow names

---

## 3. Repository Structure

```
booking-saas/
├── application/
│   ├── account/           # Boilerplate — tenant + user management (extend only)
│   ├── back-office/       # Boilerplate — support/admin tools (extend only)
│   ├── integrations/      # NEW: Apache Camel iPaaS SCS (Java 21 + Spring Boot 3)
│   ├── main/              # BookSlot core SCS (.NET 10 + React)
│   ├── shared-kernel/     # Boilerplate — extend only
│   ├── shared-webapp/     # Boilerplate — extend only
│   ├── AppHost/           # Aspire orchestration
│   └── AppGateway/        # YARP reverse proxy
├── cloud-infrastructure/  # Azure Bicep IaC
├── developer-cli/         # .NET CLI tool
├── AGENTS.md
├── DESIGN.md              # Cal.com-inspired design system (see Section 5)
└── PLAN.md                # This file
```

---

## 4. Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend SCS | .NET 10 minimal API, C# 13 |
| Architecture | Vertical Sliced Architecture (VSA) — features own their full stack |
| ORM | EF Core 10, PostgreSQL, JSONB for value collections |
| Messaging | MediatR pipeline (command/query handlers) |
| Validation | FluentValidation |
| Auth | JWT, PlatformPlatform's built-in auth pipeline |
| iPaaS SCS | Java 21, Spring Boot 3, Apache Camel 4 |
| Frontend | React 19, TypeScript 5, TanStack Router (file-based) |
| API Client | openapi-fetch (`api.useQuery`, `api.useMutation`) |
| Forms | React Aria Components + `mutationSubmitter` pattern |
| Styles | Tailwind CSS v4 |
| Build | Rsbuild (frontend), Turbo (monorepo) |
| Orchestration | .NET Aspire (AppHost) |
| Proxy | YARP (AppGateway) |
| Infra | Azure Container Apps, Azure PostgreSQL Flexible Server, Azure Key Vault, Azure Blob Storage |
| CI/CD | GitHub Actions |
| i18n | LinguiJS |

---

## 5. Design System (Cal.com-Inspired)

> Full details in `DESIGN.md`. Summary below for agent reference.

### Color Palette
| Role | Value |
|------|-------|
| Primary text | `#242424` (Charcoal) |
| Deep text | `#111111` (Midnight) |
| Secondary text | `#898989` (Mid Gray) |
| Background | `#ffffff` (Pure White) |
| Link | `#0099ff` |
| CTA button | `#242424` bg + white text |
| Shadow border | `rgba(34, 42, 53, 0.08)` ring |

### Typography
- **Headings (24px+):** Cal Sans, weight 600, line-height 1.10, tight tracking
- **Body:** Inter, weight 300–500
- Never use Cal Sans for body text. Never mix weights on Cal Sans except 600.

### Cards & Elevation
Multi-layered shadow: `rgba(19,19,22,0.7) 0px 1px 5px -4px, rgba(34,42,53,0.08) 0px 0px 0px 1px, rgba(34,42,53,0.05) 0px 4px 8px`

Use ring shadows instead of CSS `border`. No CSS gradients. No decorative illustrations.

### Spacing
Base unit 8px. Section padding 80–96px vertical. Cards 12–24px internal.

---

## 6. Architecture: iPaaS SCS (`application/integrations/`)

### Purpose
All third-party integrations route through the iPaaS. No SCS calls Twilio, PayFast, Google, or Microsoft directly — they publish internal events or call the integration layer's REST API.

### Technology
- Java 21, Spring Boot 3, Apache Camel 4
- Spring Security for internal auth (service-to-service JWT or shared secret)
- Azure Key Vault client for credential storage (Spring Cloud Azure)
- Spring Boot Actuator for health endpoints

### Structure
```
application/integrations/
├── src/main/java/com/bookslot/integrations/
│   ├── IntegrationsApplication.java
│   ├── config/
│   │   ├── CamelConfig.java
│   │   └── SecurityConfig.java
│   ├── connectors/
│   │   ├── ConnectorRegistry.java
│   │   ├── google/
│   │   │   └── GoogleCalendarRoute.java
│   │   ├── microsoft/
│   │   │   └── OutlookCalendarRoute.java
│   │   ├── twilio/
│   │   │   └── TwilioWhatsAppRoute.java
│   │   └── payfast/
│   │       └── PayFastWebhookRoute.java
│   ├── credentials/
│   │   ├── CredentialVault.java         # Azure Key Vault abstraction
│   │   └── LocalDevCredentialVault.java # AES-256 fallback for dev
│   └── api/
│       └── ConnectorController.java     # REST API for dashboard
├── src/main/resources/
│   └── application.yml
└── pom.xml (or build.gradle)
```

### REST API exposed by integrations SCS
```
GET    /api/integrations/connectors              → list all connectors + status
POST   /api/integrations/connectors/{id}/enable  → start Camel route
POST   /api/integrations/connectors/{id}/disable → stop Camel route
PUT    /api/integrations/connectors/{id}/config  → update config
POST   /api/integrations/connectors/{id}/rotate  → rotate credentials
GET    /api/integrations/connectors/{id}/health  → route health + last error
```

### Credentials Strategy
- Prod: Azure Key Vault (one secret per connector per tenant, named `{tenantId}-{connectorId}-{credentialKey}`)
- Local dev: `application-local.yml` with AES-256 encrypted values, key from env var
- Never store raw credentials in database or config files

### Aspire Integration
Add Java Spring Boot app to AppHost:
```csharp
var integrations = builder.AddSpringApp("integrations",
    workingDirectory: "../integrations",
    springApplicationName: "integrations",
    httpPort: 5003);
```

### Dashboard Frontend
Located at `application/integrations/WebApp/`. Same React + TanStack Router stack.
Routes:
- `/dashboard/integrations` — connector list, status badges, last-run stats
- `/dashboard/integrations/{id}` — config drawer, credential rotation, enable/disable

---

## 7. Domain Model Reference

All domain entities follow these rules (from `shared-kernel`):

### Backend Conventions
```csharp
// Aggregate pattern
public sealed class MyEntity : AggregateRoot<MyEntityId>, ITenantScopedEntity
{
    private MyEntity() : base(MyEntityId.NewId()) { }

    public TenantId TenantId { get; private set; } = null!;

    // ... properties with private setters

    public static MyEntity Create(...) => new MyEntity { ... };

    // Behaviour methods (not setters)
    public void DoSomething(...) { ... }
}

// Strongly typed ID
[PublicAPI]
[IdPrefix("myent")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, MyEntityId>))]
public sealed record MyEntityId(string Value) : StronglyTypedUlid<MyEntityId>(Value)
{
    public override string ToString() => Value;
}

// Repository — interface + implementation in same file under Domain/
public interface IMyEntityRepository : ICrudRepository<MyEntity, MyEntityId>
{
    Task<MyEntity?> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken);
}

internal sealed class MyEntityRepository(MainDbContext mainDbContext)
    : RepositoryBase<MyEntity, MyEntityId>(mainDbContext), IMyEntityRepository
{
    public async Task<MyEntity?> GetByTenantAsync(TenantId tenantId, CancellationToken ct)
        => await DbSet.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
}

// EF config in same file
public sealed class MyEntityConfiguration : IEntityTypeConfiguration<MyEntity> { ... }
```

### Key Infrastructure Rules
- **EF query filters:** Named filters `'Tenant'` and `'SoftDelete'`. Bypass tenant filter with `.IgnoreQueryFilters([QueryFilterNames.Tenant])` for cross-tenant queries.
- **`IExecutionContext.TenantId` being null blocks ALL tenant-filtered results.** Public (unauthenticated) APIs must use `IgnoreQueryFilters([QueryFilterNames.Tenant])`.
- **`ICrudRepository`** only exposes `GetByIdAsync`, `AddAsync`, `Update`, `Remove`. No `RemoveRange` on the interface — use individual `Remove()` calls.
- **Unit of work:** `UnitOfWorkPipelineBehavior` commits once at end of handler. Do not call `SaveChanges()` manually.
- **JSONB columns:** Use `ImmutableArray<T>` for value-object collections stored as JSONB.
- **Migrations:** One migration per feature/phase. Never combine unrelated changes.

### Existing Domain Models (reuse these designs)

#### `Appointment`
Key fields: `TenantId`, `ReferenceNumber` (`#` + 8-char ULID), `ClientName`, `ClientPhone` (normalised), `ClientEmail`, `ServiceName`, `DurationMinutes`, `Price`, `Currency`, `StartAt`, `EndAt`, `Status`, `Source`, `LocationId`, `Notes`, `InternalNotes`, `ExternalCalendarEventId`, `PaymentStatus`, `PaymentMethod`, `PaidAt`, `PaymentReference`, `ReminderSentAt`, `AssignedTeamMemberId`, `RecurringGroupId`, `Recurrence`, `VideoMeetingUrl`.

Status lifecycle (state machine):
```
New → Pending | PendingPayment | Confirmed | Cancelled
Pending → PendingPayment | Confirmed | Cancelled
PendingPayment → Confirmed | Cancelled
Confirmed → Arrived | Started | Cancelled | NoShow
Arrived → Started | Cancelled | NoShow
Started → Completed | Cancelled | NoShow
Completed → (terminal)
Cancelled → (terminal)
NoShow → (terminal)
```

Sources: `Manual`, `WhatsApp`, `CalendarSync`, `BookingPage`
Payment statuses: `Unpaid`, `PendingOnline`, `Paid`, `Refunded`
Payment methods: `Cash`, `Card`, `Online`
Reference number: `"#" + Guid.NewGuid().ToString("N").ToUpperInvariant()[..8]`
ID prefix: `appt`

#### `ServiceType`
Key fields: `TenantId`, `CategoryId`, `Name`, `Description`, `DurationMinutes`, `BufferMinutes`, `ExtraTimeBefore`, `ExtraTimeAfter`, `Price`, `Currency` (default `"ZAR"`), `PaymentTiming`, `PaymentChannel`, `DisplayOrder`, `IsActive`, `IsArchived`, `LocationIds`, `Variants`, `TimeSegments`, `Images`, `AssignedTeamMemberIds`, `TimePricingRules`, `SchedulingType`, `MinimumBookingNoticeMinutes`, `MaxAdvanceBookingDays` (default 365), `MaxActiveBookingsPerClient`, `PeriodType`, `RollingWindowDays`, `BookingPeriodStart`, `BookingPeriodEnd`, `RequiresConfirmation`, `MaxParticipantsPerSlot`, `CustomBookingFields`.

All array properties (`LocationIds`, `Variants`, `TimeSegments`, `Images`, etc.) stored as JSONB `ImmutableArray<T>`.
ID prefix: `svct`

Value object enums: `PaymentTiming` (None/Before/After), `PaymentChannel` (WhatsAppLink/CardMachine/Manual), `SchedulingType` (Manual/RoundRobin/LeastRecent), `PeriodType` (Unlimited/Rolling/DateRange), `TimeSegmentType` (Active/Processing), `BookingFieldType` (Text/Textarea/Select/Checkbox/Phone/Email)

#### `Client`
Key fields: `TenantId`, `FirstName`, `LastName`, `Phone` (normalised via `PhoneNormalizer.Normalize()`), `Email`, `AvatarUrl`, `DateOfBirth`, `Pronouns`, `Source`, `ReferredByClientId`, `IsBlocked`, `BlockedReason`, `IsDeleted`, `Notes`, `StaffAlerts`, `Allergies`, `TagIds`.
ID prefix: `clnt`

#### `Location`
Key fields: `TenantId`, `Name`, `Address`, `Phone`, `Email`, `BusinessHours`, `IsActive`.
`BusinessHours` = value object with `DayHours(Open, Close)` per day of week, stored as JSONB.
ID prefix: `loc`

#### `BusinessSchedule`
Single aggregate per tenant. Key field: `Schedule` — `ImmutableArray<DaySchedule>` (Day, IsOpen, OpenTime, CloseTime).
Default: Mon–Fri 08:00–17:00 open, Sat–Sun closed.
Table: `business_hours`
ID prefix: `bschl`

#### `BusinessProfile`
Key fields: `TenantId`, `BusinessName`, `Description`, `Phone`, `Email`, `Website`, `AddressLine1/2`, `City`, `Province`, `PostalCode`, `Country`, `WhatsAppBusinessIdentity` (JSONB), `WhatsAppNumberInventory` (JSONB array), `WhatsAppSenderProfileHistory` (JSONB array), `WhatsAppFlowDefinitions` (JSONB array).
Table: `business_profiles`
ID prefix: `bprof`
Custom repo method: `GetByTenantAsync(TenantId)`

---

## 8. Phase Roadmap

### Phase 0 — Fresh Foundation

**Goal:** Working boilerplate, renamed, all tooling green.

Tasks:
1. Clone PlatformPlatform at latest HEAD
2. Global rename: `PlatformPlatform` → `BookSlot`, `platformplatform` → `bookslot`
3. Update Azure Bicep infra scripts — add `integrations` container app resource
4. Add `integrations/` SCS placeholder to solution and AppHost
5. Verify `developer-cli build` and `developer-cli test` pass clean
6. Wire CI/CD — verify GitHub Actions pipeline green

---

### Phase 1 — iPaaS Foundation

**Goal:** Apache Camel SCS running in Aspire with a management dashboard.

**Backend (Java):**
- Spring Boot 3 app bootstrapped with Spring Initializr (Camel, Web, Actuator, Spring Cloud Azure)
- `ConnectorRegistry` — maps connector IDs to Camel route builders; supports start/stop per tenant
- `CredentialVault` interface with Azure Key Vault impl + local AES-256 dev impl
- `ConnectorController` — all REST endpoints listed in Section 6
- Health endpoint: `GET /actuator/health` + per-route custom health indicator
- Dockerfile + `aspire-manifest.json` registration

**Frontend (React):**
- `/dashboard/integrations` — table of connectors with status pill (Active / Error / Disabled)
- Connector drawer: enable toggle, config fields per connector type, credential rotation button
- Environment badge (dev / staging / prod)

**Docs:** `application/integrations/README.md` — what the SCS does, route list, credential setup

---

### Phase 2 — Core Booking Engine

Build each sub-feature as a complete vertical (backend + API + frontend + unit tests). Order matters — later ones depend on earlier.

#### 2.1 Service Types

**Backend:**
- `ServiceType` aggregate (fields listed in Section 7)
- `CreateServiceType`, `UpdateServiceType`, `DeleteServiceType`, `ReorderServiceTypes`, `ToggleServiceType` commands
- `GetServiceType`, `ListServiceTypes` queries
- `ServiceTypeRepository` (`IServiceTypeRepository : ICrudRepository<ServiceType, ServiceTypeId>`)
- `ServiceCategoryRepository` (category as separate aggregate: `ServiceCategory` with `TenantId`, `Name`, `DisplayOrder`)

**API endpoints:**
```
GET    /api/main/service-types
POST   /api/main/service-types
GET    /api/main/service-types/{id}
PUT    /api/main/service-types/{id}
DELETE /api/main/service-types/{id}
PATCH  /api/main/service-types/{id}/reorder
PATCH  /api/main/service-types/{id}/toggle
GET    /api/main/service-categories
POST   /api/main/service-categories
PUT    /api/main/service-categories/{id}
DELETE /api/main/service-categories/{id}
```

**Frontend:**
- `routes/dashboard/services.tsx` (layout, redirect to index)
- `routes/dashboard/services.index.tsx` (list with drag-to-reorder, active/archived tabs)
- `routes/dashboard/services.$id.tsx` (edit form — general, pricing, scheduling config, images)
- `routes/dashboard/services.new.tsx` (create form)

---

#### 2.2 Locations

**Backend:**
- `Location` aggregate (fields in Section 7)
- `CreateLocation`, `UpdateLocation`, `DeleteLocation`, `ToggleLocation` commands
- `GetLocation`, `ListLocations` queries
- `LocationRepository`

**API endpoints:**
```
GET    /api/main/locations
POST   /api/main/locations
GET    /api/main/locations/{id}
PUT    /api/main/locations/{id}
DELETE /api/main/locations/{id}
PATCH  /api/main/locations/{id}/toggle
```

**Frontend:**
- `routes/dashboard/locations.tsx` (layout)
- `routes/dashboard/locations.index.tsx` (list)
- `routes/dashboard/locations.$id.tsx` (edit — name, address, business hours override)

---

#### 2.3 Business Schedule

**Backend:**
- `BusinessSchedule` aggregate (single per tenant — see Section 7)
- `UpdateBusinessSchedule` command
- `GetBusinessSchedule` query (creates with defaults if not found)
- `BusinessScheduleRepository`
- `BlockedTime` aggregate — manual blocks: `TenantId`, `LocationId?`, `StartsAt`, `EndsAt`, `Reason?`, `IsRecurring`, `RecurrenceRule?`

**API endpoints:**
```
GET  /api/main/business-schedule
PUT  /api/main/business-schedule
GET  /api/main/blocked-times
POST /api/main/blocked-times
DELETE /api/main/blocked-times/{id}
```

**Frontend:**
- `routes/dashboard/schedule.tsx` (weekly hours editor, exception dates)
- Block times section inline on schedule page

---

#### 2.4 Public Booking Page

**Backend:**
- Unauthenticated API — uses `publicApi` client, `IgnoreQueryFilters([QueryFilterNames.Tenant])`
- `GetVendorProfile` query — returns business name, logo, active services, locations, timezone
- `GetAvailableSlots` query — computes free slots from schedule, blocked times, existing appointments (respects buffer, `MinimumBookingNoticeMinutes`, `MaxAdvanceBookingDays`)
- `CreateBooking` command — creates `Appointment` with `Source = BookingPage`; triggers confirmation notification via iPaaS

**Slot algorithm:**
1. Load `BusinessSchedule` for the requested date
2. Load all `Appointment`s that day (status != Cancelled) for the location/service
3. Load all `BlockedTime`s for the day
4. Generate slots every `DurationMinutes + BufferMinutes` within open hours
5. Remove conflicting slots
6. Apply `MinimumBookingNoticeMinutes` from now
7. Return available `DateTimeOffset` slots

**API endpoints (public, no auth):**
```
GET  /api/main/public/vendors/{tenantId}/profile
GET  /api/main/public/vendors/{tenantId}/services/{serviceId}/slots?date={date}
POST /api/main/public/vendors/{tenantId}/book
GET  /api/main/public/appointments/{appointmentId}         (view own appointment)
PUT  /api/main/public/appointments/{appointmentId}/cancel  (client self-cancel)
PUT  /api/main/public/appointments/{appointmentId}/reschedule
```

**Frontend:**
- `routes/book.tsx` (layout, unauthenticated)
- `routes/book.$tenantId.tsx` (vendor landing, service list)
- `routes/book.$tenantId.$serviceId.tsx` (slot picker, booking form, opt-in capture)
- `routes/book.confirmation.$appointmentId.tsx` (success page)
- `routes/book.manage.$appointmentId.tsx` (cancel / reschedule)

---

#### 2.5 Appointments

**Backend:**
- `CreateAppointment` (manual), `UpdateAppointment`, `CancelAppointment`, `RescheduleAppointment` commands
- `ConfirmAppointment`, `MarkArrived`, `StartAppointment`, `CompleteAppointment`, `MarkNoShow` commands
- `GetAppointment`, `ListAppointments` (with filters: status, date range, service, location, client), `GetCalendarEvents` queries
- `AppointmentRepository`

**API endpoints:**
```
GET    /api/main/appointments
POST   /api/main/appointments
GET    /api/main/appointments/{id}
PUT    /api/main/appointments/{id}
DELETE /api/main/appointments/{id}
PATCH  /api/main/appointments/{id}/status   (confirm/arrive/start/complete/no-show/cancel)
PATCH  /api/main/appointments/{id}/reschedule
GET    /api/main/appointments/calendar      (date range, for calendar view)
```

**Frontend:**
- `routes/dashboard/appointments.tsx` (layout)
- `routes/dashboard/appointments.index.tsx` (calendar view + list view toggle)
- `routes/dashboard/appointments.$id.tsx` (detail/edit panel)
- `routes/dashboard/appointments.new.tsx` (manual booking form)

---

#### 2.6 Clients

**Backend:**
- `Client` aggregate (fields in Section 7)
- `CreateClient`, `UpdateClient`, `DeleteClient`, `BlockClient`, `UnblockClient` commands
- `AddClientNote`, `RemoveClientNote`, `AddClientAlert` commands
- `GetClient`, `ListClients`, `SearchClients` queries
- `ClientRepository`
- `ClientTag` aggregate (separate, `TenantId`, `Name`, `Color`)

**API endpoints:**
```
GET    /api/main/clients
POST   /api/main/clients
GET    /api/main/clients/{id}
PUT    /api/main/clients/{id}
DELETE /api/main/clients/{id}
PATCH  /api/main/clients/{id}/block
PATCH  /api/main/clients/{id}/unblock
POST   /api/main/clients/{id}/notes
DELETE /api/main/clients/{id}/notes/{noteId}
GET    /api/main/client-tags
POST   /api/main/client-tags
```

**Frontend:**
- `routes/dashboard/clients.tsx` (layout)
- `routes/dashboard/clients.index.tsx` (list, search, filter by tags)
- `routes/dashboard/clients.$id.tsx` (profile: details, appointment history, notes, alerts)
- `routes/dashboard/clients.new.tsx`

---

### Phase 3 — Business Profile & Onboarding

**Backend:**
- `BusinessProfile` aggregate (fields in Section 7 — **without WhatsApp fields initially**)
- `CreateBusinessProfile`, `UpdateBusinessProfile` commands
- `GetBusinessProfile` query (upsert on first access)
- `BusinessProfileRepository` with `GetByTenantAsync`

**API:**
```
GET  /api/main/business-profile
PUT  /api/main/business-profile
POST /api/main/business-profile/logo    (blob upload)
```

**Onboarding flow (frontend):**
- `routes/onboarding.tsx` (layout)
- `routes/onboarding.profile.tsx` → `routes/onboarding.service.tsx` → `routes/onboarding.location.tsx` → `routes/onboarding.schedule.tsx` → `routes/onboarding.complete.tsx`
- Onboarding step tracked on tenant record (extend account SCS or use main SCS preference)

**Settings frontend:**
- `routes/dashboard/settings.tsx` (layout, redirects to `.profile`)
- `routes/dashboard/settings.profile.tsx`
- `routes/dashboard/settings.booking.tsx` (booking page customisation — cancellation policy, branding colour, welcome message)
- `routes/dashboard/settings.notifications.tsx` (notification preferences — which events trigger messages)

---

### Phase 4 — Calendar Integration (via iPaaS)

**Camel Routes (integrations SCS):**

`GoogleCalendarRoute`:
- OAuth2 PKCE flow — redirect URI points back to `main` API, code exchange happens server-side
- Token refresh via Camel timer + Azure Key Vault token storage
- Polls for new/changed events every 5 minutes (or push via Google webhook if available)
- Translates Google Event → `CalendarEvent` internal DTO, pushes to `POST /api/main/calendar/events/inbound`
- Writes BookSlot appointment to Google on `CalendarEventOutbound` message from `main`

`OutlookCalendarRoute`:
- Same pattern via MS Graph API
- Subscription-based push (MS Graph change notifications) preferred over polling

**main SCS additions:**

`CalendarSettings` aggregate:
- `TenantId`, `Provider` (Google/Outlook), `ConnectedCalendarId`, `AccessTokenRef` (Key Vault reference), `RefreshTokenRef`, `TokenExpiresAt`, `SyncEnabled`, `SyncDirection` (TwoWay/ImportOnly/ExportOnly), `ConnectedAt`, `LastSyncAt`, `LastSyncError`
- ID prefix: `calset`

`CalendarSync` feature:
- `HandleInboundCalendarEvent` command — receives event from iPaaS, creates/updates `BlockedTime` or matches to existing appointment
- `SyncAppointmentToCalendar` command — called from appointment lifecycle events, sends to iPaaS outbound queue
- `GetCalendarSyncStatus` query

API:
```
GET    /api/main/calendar-settings
POST   /api/main/calendar-settings/google/connect    (initiates OAuth flow)
GET    /api/main/calendar-settings/google/callback   (OAuth callback)
POST   /api/main/calendar-settings/outlook/connect
GET    /api/main/calendar-settings/outlook/callback
DELETE /api/main/calendar-settings/{provider}        (disconnect)
POST   /api/main/calendar/events/inbound             (iPaaS → main, internal)
```

Frontend:
- `routes/dashboard/settings.calendar.tsx` — connect/disconnect Google + Outlook, sync status, last error

---

### Phase 5 — PayFast Payments

**Camel Route (integrations SCS):**

`PayFastWebhookRoute`:
- Listens on `POST /api/integrations/payfast/itn`
- Verifies signature: MD5 hash of sorted `key=value` pairs + passphrase (from credential vault)
- Checks `payment_status` field: `COMPLETE` → forward to `POST /api/main/payments/payfast/itn`
- Idempotency: hash the `m_payment_id` — skip if already processed
- Dead-letter queue for failed verifications

**main SCS:**

`Payment` aggregate:
- `TenantId`, `AppointmentId`, `Amount`, `Currency`, `PayFastPaymentId` (their `pf_payment_id`), `MerchantPaymentId` (our generated ID), `Status` (Pending/Captured/Failed/Refunded), `CapturedAt`, `RefundedAt`, `RefundReason`, `PayFastRawResponse` (JSONB)
- ID prefix: `pay`

`HandlePayFastItn` command — updates `Payment` and transitions `Appointment` to `Confirmed` (or `Cancelled` on failure)

`InitiatePayment` command — generates `MerchantPaymentId`, builds PayFast payment URL, returns redirect URL

`RefundPayment` command — calls PayFast refund API (via iPaaS HTTP route)

**PayFast integration specifics:**
- Sandbox URL: `https://sandbox.payfast.co.za/eng/process`
- Live URL: `https://www.payfast.co.za/eng/process`
- Merchant ID + Merchant Key + Passphrase stored in iPaaS credential vault
- ITN webhook endpoint: `POST /api/main/payments/payfast/itn` (no auth, signature-verified)
- Payment required if `ServiceType.Price > 0` and `ServiceType.PaymentTiming = Before`
- Booking flow: slot selection → booking form → redirect to PayFast → ITN → confirmation

**API endpoints:**
```
POST /api/main/payments/initiate             (creates payment intent, returns redirect URL)
POST /api/main/payments/payfast/itn          (PayFast webhook — no auth)
POST /api/main/payments/{id}/refund
GET  /api/main/payments                      (list for dashboard)
GET  /api/main/payments/{id}
```

**Frontend:**
- `routes/book.payment-callback.tsx` (return URL from PayFast — show loading, poll for payment status)
- `routes/dashboard/payments.tsx` (transaction list, filters, refund actions)
- Payment step injected into `book.$tenantId.$serviceId.tsx` when `Price > 0`

---

### Phase 6 — WhatsApp Notifications (via iPaaS)

**Non-negotiable rules:**
1. **Never send free-text** to a client outside a 24-hour inbound session window. Always use `ContentSid`.
2. **Opt-in required.** No opt-in record → log + return (no throw, no silent drop).
3. **Single aggregate** — no dual source-of-truth. `WhatsAppOnboarding` on `BusinessProfile` (not a separate `WhatsAppSettings` table).
4. **Platform-owned templates v1** — 4 templates submitted once on BookSlot's WABA, SIDs hard-coded per environment.

**Camel Route (integrations SCS):**

`TwilioWhatsAppRoute`:
- `SendWhatsAppMessage` message processor:
  1. Load opt-in from `POST /api/main/whatsapp/opt-in/check`
  2. If opted-in AND `now - LastInboundAt < 24h` → send free-text body
  3. Otherwise → send with `ContentSid` + `ContentVariables`
  4. Idempotency key: `tenant-{id}-appt-{id}-{eventType}`
- `HandleInboundMessage` — receives Twilio webhook, updates `LastInboundAt`, creates `WhatsAppMessageLog`
- Webhook endpoint: `POST /api/integrations/twilio/whatsapp/inbound` (Twilio signature verified)
- Polly retry: 3 attempts, exponential backoff, Twilio 429 → circuit breaker

**main SCS:**

`WhatsAppOptIn` aggregate:
- `TenantId`, `ClientPhone` (normalised), `ConsentedAt`, `ConsentSource` (BookingPage/Manual/Import), `ConsentPurpose`, `RevokedAt?`, `LastInboundAt?`
- ID prefix: `woptin`
- Repository method: `GetByPhoneAsync(TenantId, string phone)`

`WhatsAppOnboarding` — **on `BusinessProfile`** (not separate aggregate):
- Fields: `TwilioSubaccountSid`, `TwilioSubaccountAuthToken` (encrypted via `ITokenEncryptor`), `PhoneNumber`, `TwilioPhoneNumberSid`, `WabaId`, `SenderSid`, `OnboardingStatus` (NotStarted/NumberReserved/EmbeddedSignupCompleted/Active/Failed), `NumberLifecycleStatus` (Reserved/Active/Released), `DisplayName`

`WhatsAppMessageLog` aggregate:
- `TenantId`, `AppointmentId?`, `ClientPhone`, `MessageType` (Confirmation/Cancellation/Reschedule/Reminder/Custom), `Status` (Sent/Failed/Blocked), `BlockReason?` (NoOptIn/SessionExpired), `ContentSid?`, `TwilioMessageSid?`, `SentAt?`, `ErrorCode?`
- ID prefix: `wamsg`

**4 Platform Templates (v1 — hard-coded ContentSIDs):**
| Template | Variables |
|----------|-----------|
| `bookslot_confirmation` | `{{business_name}}`, `{{service_name}}`, `{{date_time}}`, `{{reference}}` |
| `bookslot_cancellation` | `{{business_name}}`, `{{service_name}}`, `{{date_time}}`, `{{reference}}` |
| `bookslot_reschedule` | `{{business_name}}`, `{{service_name}}`, `{{old_date_time}}`, `{{new_date_time}}`, `{{reference}}` |
| `bookslot_reminder` | `{{business_name}}`, `{{service_name}}`, `{{date_time}}`, `{{reference}}`, `{{hours_until}}` |

**API endpoints:**
```
GET    /api/main/whatsapp/onboarding          (get current onboarding state)
POST   /api/main/whatsapp/senders/search      (list available Twilio numbers by country)
POST   /api/main/whatsapp/senders/reserve     (purchase Twilio number + create subaccount)
POST   /api/main/whatsapp/senders/complete    (post embedded-signup callback → register sender)
POST   /api/main/whatsapp/senders/check       (poll sender registration status)
DELETE /api/main/whatsapp/senders             (disconnect + release Twilio number)
POST   /api/main/whatsapp/opt-in/check        (internal — called by iPaaS)
POST   /api/main/whatsapp/opt-in/record       (record opt-in from booking page)
DELETE /api/main/whatsapp/opt-in/{phone}      (revoke opt-in)
POST   /api/main/whatsapp/test-send           (send test template to authenticated user's phone)
```

**Frontend:**
- `routes/dashboard/connectors.tsx` (layout)
- `routes/dashboard/connectors.whatsapp.tsx` — onboarding wizard + sender status + opt-in stats
- Opt-in checkbox in `book.$tenantId.$serviceId.tsx` — captures consent before booking

---

### Phase 7 — Advanced Features

Build in priority order:

#### 7.1 Waitlist
`Waitlist` aggregate: `TenantId`, `ServiceTypeId`, `LocationId?`, `PreferredDate`, `ClientName`, `ClientPhone`, `ClientEmail?`, `Status` (Waiting/Notified/Booked/Expired), `NotifiedAt?`, `ExpiresAt`.
- Triggered when slot fills: add to waitlist
- On cancellation: notify first in queue via WhatsApp if enabled
- ID prefix: `wait`

#### 7.2 Insights
Read-only queries only — no aggregate. Backed by EF Core `InMemoryDatabase` for unit tests (not SQLite, due to named filter translation issues with StronglyTypedId types).
- `GetBookingInsights` — total bookings, cancellation rate, completion rate by period
- `GetRevenueInsights` — revenue by service, by location, by period
- `GetClientInsights` — new clients, retention rate, top clients by booking count
- Frontend: `routes/dashboard/insights.tsx` — charts, date range picker

#### 7.3 API Keys
`ApiKey` aggregate: `TenantId`, `Name`, `KeyHash` (SHA-256 of raw key), `Prefix` (first 8 chars for display), `Scopes`, `ExpiresAt?`, `LastUsedAt?`, `IsRevoked`.
- Key only returned once on creation
- ID prefix: `apikey`

#### 7.4 Webhooks
`WebhookSubscription` aggregate: `TenantId`, `Url`, `Secret` (for HMAC-SHA256 signature header), `Events` (array of event names), `IsActive`, `FailureCount`.
`WebhookDelivery` aggregate: `SubscriptionId`, `Event`, `Payload` (JSONB), `Status` (Pending/Delivered/Failed), `AttemptCount`, `NextAttemptAt`, `LastAttemptAt`, `ResponseStatus`.
- Delivery via background worker with exponential backoff (max 5 attempts)
- Events: `appointment.created`, `appointment.confirmed`, `appointment.cancelled`, `appointment.completed`, `payment.captured`, `payment.refunded`

---

### Phase 8 — Back-office & Platform Operations

Extend the boilerplate back-office SCS:

- **Tenant management** — list tenants, view details, suspend/unsuspend, impersonate
- **Integration health** — read `GET /api/integrations/connectors/{id}/health` from back-office
- **Platform metrics** — aggregate booking counts, payment volumes, active tenants
- **Audit log viewer** — search domain event log by tenant, event type, date range
- **WhatsApp template status** — view approval states of platform templates across Twilio

---

## 9. Frontend Conventions

### Routing
TanStack Router file-based routing. Layout file (`settings.tsx`) + child files (`settings.profile.tsx`). Parent uses `useLocation()` to redirect to first child when at exact parent path.

### API Calls
```tsx
// Authenticated
import { api } from "@/shared/lib/api/client";
const { data } = api.useQuery("get", "/api/main/service-types");
const mutation = api.useMutation("post", "/api/main/service-types");

// Unauthenticated (public booking page)
import { publicApi } from "@/shared/lib/api/publicClient";
const { data } = publicApi.useQuery("get", "/api/main/public/vendors/{tenantId}/profile", {
  params: { path: { tenantId } }
});

// Invalidate after mutation
queryClient.invalidateQueries({ queryKey: ["get", "/api/main/service-types"] });
```

### Forms
```tsx
<Form onSubmit={mutationSubmitter(mutation, params)}>
  <TextField name="name" label="Service name" isRequired />
  <Button type="submit">Save</Button>
</Form>
```
Mutation variables must be typed as `MutationParams` from `@repo/ui/forms/mutationSubmitter`.

### Settings Pattern
Parent layout file redirects to first child:
```tsx
// settings.tsx
const { pathname } = useLocation();
if (pathname === "/dashboard/settings") return <Navigate to="/dashboard/settings/profile" />;
```

---

## 10. Backend Handler Conventions

```csharp
// Command handler
public sealed record CreateServiceTypeCommand(...) : ICommand<ServiceTypeId>;

public sealed class CreateServiceTypeHandler(
    IServiceTypeRepository repository,
    IExecutionContext executionContext
) : ICommandHandler<CreateServiceTypeCommand, ServiceTypeId>
{
    public async Task<Result<ServiceTypeId>> Handle(
        CreateServiceTypeCommand command,
        CancellationToken cancellationToken)
    {
        var serviceType = ServiceType.Create(
            executionContext.TenantId!,
            ...
        );
        await repository.AddAsync(serviceType, cancellationToken);
        return serviceType.Id;
    }
}

// Validator
public sealed class CreateServiceTypeValidator : AbstractValidator<CreateServiceTypeCommand>
{
    public CreateServiceTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DurationMinutes).GreaterThan(0);
    }
}
```

### Testing (unit tests, `application/main/Tests/`)
- Use `SqliteInMemoryDbContextFactory<MainDbContext>` for handlers that use `DbContext`
- Use `EF Core InMemoryDatabase` for Insights/query handlers (SQLite cannot handle `DateTimeOffset` range comparisons or named filter translation with StronglyTypedId types)
- Always test: happy path, validation failure, not-found, permission denied (wrong tenant)

---

## 11. Agent Workflow (Mandatory)

The project supports two parallel agentic modes. Both use the same `.claude/agents/*.md` files and `developer-cli` MCP tools. **Never implement code as coordinator in either mode.**

### Mode A: GitHub Copilot CLI (Primary — no setup required)

GitHub Copilot CLI acts as coordinator. Dispatch sub-agents via the built-in `task` tool:

```
User → Copilot CLI (coordinator)
  → task(agent_type="backend-engineer", prompt="...")  → implements directly, full toolset
  → task(agent_type="backend-reviewer", prompt="...")  → reviews diff, returns approval/feedback
```

No terminal setup. No worker-host processes. Agents are stateless per dispatch — embed full context (relevant PLAN.md section + task description) in each prompt.

### Mode B: Claude Code Worker-Host (Optional — persistent session memory)

For developers using Claude Code who want persistent agent memory across multi-task features:

```bash
pp claude-agent coordinator      # terminal 1
pp claude-agent backend-engineer # terminal 2 (keeps memory between tasks)
```

The developer-cli `start_worker_agent` / `complete_work` / `claude-agent` infrastructure remains available for this mode. Worker-host agents read `.claude/commands/process/implement-task.md` and maintain session context via `.claude-session-id`.

### Delegation Table (both modes)

| Work type | Agent | Reviewer |
|-----------|-------|----------|
| .NET backend feature | `backend-engineer` | `backend-reviewer` |
| React/TS frontend feature | `frontend-engineer` | `frontend-reviewer` |
| Playwright E2E tests | `qa-engineer` | `qa-reviewer` |

### Delegation Rules (both modes)
- One SCS per agent call — never split `main` backend + `integrations` Java in one task
- Always pass the relevant PLAN.md section as context in the task prompt
- Engineer → Reviewer pipeline is mandatory before marking a task done
- Reviewer returns `✅ APPROVED` or `❌ CHANGES REQUIRED` with specific file:line references

### Build/Test Commands (via MCP, both modes — never raw `dotnet`/`npm`)
```
execute_command(command='build', backend=true, frontend=true)
execute_command(command='test', backend=true, noBuild=true)
execute_command(command='format', backend=true, noBuild=true)
execute_command(command='lint', frontend=true, noBuild=true)
```

Slow operations (Aspire restart, backend format/lint, E2E) → dispatch as parallel `task` agents.

---

## 12. What Was Wrong in the Previous Codebase (Do Not Repeat)

| Problem | Fix in new codebase |
|---------|---------------------|
| `WhatsAppSettings` aggregate duplicated fields from `BusinessProfile` | Single `WhatsAppOnboarding` embedded in `BusinessProfile` |
| Three separate WhatsApp status enums with non-1:1 mappings | Single `OnboardingStatus` + `LifecycleStatus` per inventory entry |
| Twilio sends free-text `Body` to all appointments — rejected outside 24h window | Always use `ContentSid` for business-initiated messages |
| Zero opt-in / consent tracking | `WhatsAppOptIn` aggregate, POPIA-compliant |
| Nango inline in `main` Core/Integrations | All third-party calls go through iPaaS Camel routes |
| Paystack wired directly in `main` Core | PayFast via iPaaS Camel route + ITN webhook verification |
| No idempotency on Twilio or payment calls | Idempotency keys on all external calls |
| `DisconnectWhatsApp` didn't call Twilio — tenant kept paying | Disconnect releases Twilio number via iPaaS route |
| WhatsApp "flows" UI accepted config but backend never read it at send time | No flow UI until backend is fully implemented |
| Code generated without agent discipline — inconsistent patterns | All work via `backend-engineer` → `backend-reviewer` pipeline |

---

## 13. ID Prefix Registry

| Entity | Prefix |
|--------|--------|
| `Appointment` | `appt` |
| `ServiceType` | `svct` |
| `ServiceVariant` | `svcv` |
| `ServiceTimeSegment` | `svts` |
| `ServiceImage` | `svci` |
| `TimePricingRule` | `svpr` |
| `ServiceCategory` | `svccat` |
| `Client` | `clnt` |
| `Location` | `loc` |
| `BusinessProfile` | `bprof` |
| `BusinessSchedule` | `bschl` |
| `BlockedTime` | `blkt` |
| `CalendarSettings` | `calset` |
| `Payment` | `pay` |
| `WhatsAppOptIn` | `woptin` |
| `WhatsAppMessageLog` | `wamsg` |
| `Waitlist` | `wait` |
| `ApiKey` | `apikey` |
| `WebhookSubscription` | `whksub` |
| `WebhookDelivery` | `whkdlv` |
| `ClientTag` | `ctag` |

---

## 14. Open Decisions (Resolve Before Building Each Phase)

1. **Phase 1:** Which Azure region for prod? (affects Aspire env vars and Key Vault name)
2. **Phase 4:** Google Calendar push notifications require a publicly reachable endpoint. In dev, use `ngrok` or Aspire tunnel. Document setup in `integrations/README.md`.
3. **Phase 5:** PayFast does not support direct refund API in all scenarios — confirm refund flow with PayFast sandbox before building the command.
4. **Phase 6:** Platform WhatsApp templates must be submitted to Meta for approval before they can be used. Allow minimum 2 weeks for approval. Build the send path with template SIDs as config values, not hard-coded strings, so they can be swapped after approval.
5. **Phase 7 (Webhooks):** Worker for delivery retries — extend existing `Workers` project in `main` SCS using the boilerplate's job runner pattern.

---

## 15. Agent File Setup (Critical — Do Before Phase 0)

The PlatformPlatform boilerplate ships `.claude/agents/*.md` as **Claude Code worker-host passthrough proxies** (`tools: mcp__developer-cli__start_worker_agent` only). These must be replaced with **real implementation agents** so both Copilot CLI and Claude Code can dispatch them directly.

### Why the Change

| Current (boilerplate) | Target (new codebase) |
|----------------------|----------------------|
| `tools: mcp__developer-cli__start_worker_agent` only | No `tools:` restriction — inherits all available tools |
| Pure passthrough — zero thinking | Real implementation — reads rules, writes code, builds, tests |
| Requires Claude Code worker-host running in terminal | Works via `task` tool in Copilot CLI with no setup |
| Claude Code only | Both Copilot CLI and Claude Code |

### Frontmatter Template (all agents)

```yaml
---
name: backend-engineer
description: Called by coordinator for backend development tasks.
model: claude-sonnet-4-5
color: green
---
```

**No `tools:` line.** Removing it gives the agent all tools available in the dispatching system.

### backend-engineer.md

```markdown
You are a **backend engineer** in the BookSlot project implementing vertical-slice features in .NET 10.

## Role
- Implement commands, queries, domain models, repositories, API endpoints, and xUnit tests
- One task = one commit. All subtasks land together — code must compile, run, and pass tests
- Build and test incrementally after each meaningful change, not only at the end
- When complete, delegate to `backend-reviewer`

## Before Any Implementation
Read these rule files:
- `.claude/rules/backend/backend.md`
- `.claude/rules/backend/domain-modeling.md`
- `.claude/rules/backend/commands.md`
- `.claude/rules/backend/queries.md`
- `.claude/rules/backend/api-endpoints.md`
- `.claude/rules/backend/api-tests.md`
- `.claude/rules/backend/database-migrations.md`

## Mandatory Validation (before calling reviewer)
Run in order — all must pass with zero failures/warnings:
1. `execute_command(command='build', backend=true)`
2. `execute_command(command='test', backend=true, noBuild=true)`
3. `execute_command(command='format', backend=true, noBuild=true)`

## Completion
Commit with message in imperative form. Then call reviewer:
`task(agent_type="backend-reviewer", prompt="Review: [what was implemented] on branch [branch]")`
```

### frontend-engineer.md

```markdown
You are a **frontend engineer** in the BookSlot project implementing React/TypeScript features.

## Role
- Implement TanStack Router routes, React components, API integration, and translations
- One task = one commit. All subtasks land together
- Test in browser via Playwright MCP — zero tolerance for visual regressions
- When complete, delegate to `frontend-reviewer`

## Before Any Implementation
Read these rule files:
- `.claude/rules/frontend/frontend.md`
- `.claude/rules/frontend/tanstack-query-api-integration.md`
- `.claude/rules/frontend/form-with-validation.md`
- `.claude/rules/frontend/translations.md`

## Mandatory Validation (before calling reviewer)
1. `execute_command(command='build', frontend=true)`
2. `execute_command(command='lint', frontend=true, noBuild=true)`
3. Visual browser check via Playwright MCP — no layout breaks, no console errors

## Completion
Commit. Then call reviewer:
`task(agent_type="frontend-reviewer", prompt="Review: [what was implemented] on branch [branch]")`
```

### backend-reviewer.md

```markdown
You are a **backend reviewer** in the BookSlot project. Review .NET backend code for correctness and convention compliance. Never implement — return feedback only.

## Review Checklist
- [ ] Aggregate: `AggregateRoot<T>`, private ctor, static `Create` factory, `IEntityTypeConfiguration` in same file
- [ ] ID: `StronglyTypedUlid` with correct `[IdPrefix("xxx")]` — check PLAN.md Section 13
- [ ] Repository: `ICrudRepository<T>` interface + `RepositoryBase` implementation in same file under `Domain/`
- [ ] Handler: never calls `SaveChanges()` directly (UnitOfWork pipeline handles it)
- [ ] Validator: covers all required fields with sensible max lengths
- [ ] API endpoint: correct HTTP method, registered in correct endpoint group, auth applied
- [ ] Tests: happy path + validation failure + not-found + wrong-tenant minimum
- [ ] No direct external API calls — all third-party traffic via iPaaS Camel routes
- [ ] No `WhatsApp`, `PayFast`, or `Calendar` logic in `main` Core directly

## Output
Return exactly one of:
- `✅ APPROVED — [brief summary of what was implemented]`
- `❌ CHANGES REQUIRED — [specific issues with file:line references]`
```

### frontend-reviewer.md

```markdown
You are a **frontend reviewer** in the BookSlot project. Review React/TypeScript code. Never implement — return feedback only.

## Review Checklist
- [ ] TanStack Router routing: correct file name convention (`settings.profile.tsx` not `settings/profile.tsx`)
- [ ] API calls: uses `api.useQuery`/`api.useMutation` from `@/shared/lib/api/client` (authenticated) or `publicApi` from `publicClient` (public)
- [ ] Forms: uses `<Form onSubmit={mutationSubmitter(...)}>` pattern with `MutationParams` typing
- [ ] No raw `fetch` or `axios` calls
- [ ] Translations: all user-visible strings use `t()` — no hard-coded English strings in JSX
- [ ] No console errors in browser
- [ ] Responsive — works at 375px (mobile) and 1280px (desktop)

## Output
- `✅ APPROVED — [brief summary]`
- `❌ CHANGES REQUIRED — [specific issues]`
```

### qa-engineer.md

```markdown
You are a **QA engineer** in the BookSlot project writing Playwright E2E tests.

## Role
- Write Playwright tests in `application/main/WebApp/tests/e2e/` (or the relevant SCS)
- Tests must pass against a running Aspire instance (`developer-cli run`)
- Follow `.claude/rules/end-to-end-tests/end-to-end-tests.md`
- When complete, delegate to `qa-reviewer`

## Mandatory Validation
1. `end_to_end(searchTerms=["your-test-file"])` — all new tests must pass
2. `end_to_end()` — full suite must not regress

## Completion
`task(agent_type="qa-reviewer", prompt="Review E2E tests: [what was tested] on branch [branch]")`
```

### qa-reviewer.md

```markdown
You are a **QA reviewer** in the BookSlot project. Review Playwright E2E tests for coverage and reliability. Never implement — return feedback only.

## Review Checklist
- [ ] Tests cover the happy path end-to-end (fill form → submit → see result)
- [ ] Tests cover at least one error/validation path
- [ ] No `page.waitForTimeout()` — use `waitForSelector` or `waitForResponse`
- [ ] Test IDs use `data-testid` attributes, not CSS class selectors
- [ ] Tests are isolated — no shared state between tests

## Output
- `✅ APPROVED — [brief summary]`
- `❌ CHANGES REQUIRED — [specific issues]`
```

### pair-programmer.md

```markdown
You are a **pair programmer** collaborating directly with the developer. You have full tool access.

Work interactively — ask for clarification, propose approaches before implementing, explain your reasoning. You are not bound by the engineer/reviewer pipeline. Commit only when the developer asks.
```

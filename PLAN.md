# Nerova Bookings — Master Product & Technical Plan

> This file is the authoritative reference for what we are building, why, and how in the current Nerova Bookings repository.

---

## 1. Product Vision

**Nerova Bookings** is a SaaS appointment-booking platform built for non-technical professional business owners in South Africa (primary), UK, US, and Australia.

### Core Philosophy: "Buy and Use"

> *"The entire idea of the app is providing a ready-built system for non-technical professional business owners. No one wants to invest time to configure a random flow. It's buy, and use. That is it."*

This means:
- **Zero manual configuration** for any feature. Everything is pre-built and activated via simple toggles.
- **No flow builders**, no drag-and-drop tools, no custom template editors.
- Every integration (WhatsApp, calendar, payments) must work out-of-the-box the moment a tenant enables it.
- The UI is designed for confidence, not complexity.

**Interpretation:** "Zero manual configuration" does not mean the owner never enters business data. It means the app avoids arbitrary workflow builders and expert-only setup. Services, hours, staff, payment preferences, WhatsApp sender setup, and calendar connections are configured through guided defaults and short setup screens.

**Primary booking surface:** bookings happen inside each tenant's WhatsApp chatbot using professional, prebuilt WhatsApp Flows. Nerova Bookings does not rely on a platform-hosted public booking page as the primary customer experience. The dashboard is for business owners and staff; WhatsApp is for clients.

### Target Markets
- **Primary:** African Countries, especially South Africa (POPIA compliance, WhatsApp ubiquity, PayFast dominance)
- **Secondary:** UK, US, Australia (GDPR compliance, Twilio/Paystack support)

### Compliance Notes
- **POPIA (ZA):** Opt-in for WhatsApp must capture purpose + timestamp + consent source
- **GDPR (UK/EU):** Same requirements where applicable
- **No live tenants yet** — dev/staging only. Migrations can be drop-and-rebuild without backfill.

### MVP Scope Guardrails

The first usable release must prove the core booking loop before platform-level integration infrastructure is built.

**MVP focus:**
- Business onboarding with sensible defaults
- Services
- Business hours and blocked times
- Per-tenant WhatsApp chatbot setup
- Professional WhatsApp Flow-based booking journey
- WhatsApp inbound message and Flow submission handling
- Appointments dashboard
- Clients created from bookings
- PayFast upfront payment support for paid services
- WhatsApp booking confirmation, cancellation, reminder, and management messages
- Nango-powered calendar connection for busy-time import only

**Explicitly deferred until after MVP:**
- Platform-hosted public web booking page
- Self-built Apache Camel iPaaS
- Full integrations dashboard
- Full two-way Google/Outlook calendar sync
- External calendar event writes and conflict-resolution UI
- Waitlist
- Insights
- API keys
- Webhooks
- Advanced connector health operations

**MVP review questions to answer before implementation:**
1. Should MVP support solo businesses only, or must it include staff/team scheduling from day one?
2. Are locations required in MVP, or can MVP launch with one default location plus business address?
3. Is PayFast required for every paid booking at launch, or can MVP support unpaid bookings first and paid bookings shortly after?
4. Should calendar integration only block unavailable time in MVP, with outbound event creation deferred?
5. Should each tenant use a tenant-owned WhatsApp sender from day one, or can MVP use a platform-owned sender while onboarding is proven?
6. What parts of the WhatsApp Flow are customizable in MVP: branding/tone only, service-specific questions, cancellation policy, or custom fields?
7. Should appointment management happen entirely in WhatsApp, or should WhatsApp send a secure fallback management link for complex changes?
8. What countries are truly launch markets for MVP: South Africa only, or South Africa plus one secondary market?

---

## 2. Repository Base

This repository is based on [PlatformPlatform](https://github.com/platformplatform/PlatformPlatform). Do not restart by cloning a new codebase unless explicitly decided later. Stabilise and extend the current repository.

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

**Rename checklist (PlatformPlatform → Nerova Bookings):**
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
│   ├── integrations/      # POST-MVP: Apache Camel iPaaS SCS (Java 21 + Spring Boot 3)
│   ├── main/              # Nerova Bookings core SCS (.NET 10 + React)
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
| MVP integrations | Nango for Google/Outlook calendar OAuth, proxying, and busy-time sync |
| Post-MVP iPaaS SCS | Java 21, Spring Boot 3, Apache Camel 4 |
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

## 6. Architecture: Integrations

### Purpose
Integrations must be isolated behind an internal boundary so the booking domain does not become coupled to third-party SDKs or provider-specific API details.

For MVP, calendar integrations use **Nango** for OAuth, token refresh, provider proxying, and sync/webhook lifecycle. The self-built Apache Camel iPaaS remains the post-MVP target if integration complexity, scale, or cost justifies owning that infrastructure.

No domain handler should call Google or Microsoft directly. Handlers call an internal calendar integration abstraction, which is implemented with Nango for MVP and can later be backed by a self-built iPaaS.

WhatsApp is the primary customer booking channel. MVP uses an internal WhatsApp booking boundary for inbound messages, Flow delivery, Flow submissions, opt-in, booking creation, payment handoff, and appointment management. If multi-provider messaging or tenant-owned sender operations become complex enough, move this boundary behind the same post-MVP iPaaS.

PayFast remains a direct platform payment integration because it is core to the South African product and not a tenant-managed connector.

### MVP: Nango Calendar Boundary

MVP calendar scope is **busy-time import only**:
- User connects Google Calendar or Outlook via Nango.
- `main` stores the Nango connection ID, provider, selected calendar, status, last sync timestamp, and last error.
- Nango sync/webhook events notify `main`.
- `main` imports external busy periods into internal calendar-block records.
- Slot availability checks internal appointments, blocked times, and imported busy periods.
- MVP does not write Nerova appointments back to external calendars unless explicitly approved later.

### MVP: WhatsApp Booking Boundary

MVP WhatsApp scope:
- Each tenant has a WhatsApp chatbot entry point.
- The chatbot sends professional, prebuilt WhatsApp Flows for service selection, slot selection, client details, opt-in, and confirmation.
- Flow structure is generated from Nerova service, location, schedule, payment, and business-profile data. Tenants do not build arbitrary flows.
- Flow customization is limited to safe business settings such as display name, logo/profile data, service-specific booking questions, cancellation policy, and tone. The core flow logic remains platform-owned.
- Flow submissions call `main`, which validates availability, creates appointments, creates clients, records opt-in, and starts payment where required.
- Appointment management happens through WhatsApp conversation steps and/or secure management tokens when a fallback link is unavoidable.

### Post-MVP: Self-Built iPaaS SCS (`application/integrations/`)

### Technology
- Java 21, Spring Boot 3, Apache Camel 4
- Spring Security for internal auth (service-to-service JWT or shared secret)
- Azure Key Vault client for credential storage (Spring Cloud Azure)
- Spring Boot Actuator for health endpoints

### Structure
```
application/integrations/
├── src/main/java/com/nerovabookings/integrations/
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
│   │   └── (payfast connector deferred — not an iPaaS route)
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

Sources: `Manual`, `WhatsApp`, `WhatsAppFlow`, `CalendarSync`, `WebFallback` (post-MVP only)
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

#### `TeamMember` (Decision Required)
The plan references `AssignedTeamMemberId`, `AssignedTeamMemberIds`, `RoundRobin`, and `LeastRecent` scheduling. These require an explicit team/staff model before advanced availability can be correct.

MVP options:
- **Solo-only MVP:** remove team-member fields from MVP availability and add staff scheduling later.
- **Basic staff MVP:** add `TeamMember` with `TenantId`, `Name`, `Email`, `Phone`, `IsActive`, assigned services, assigned locations, and working hours override.

Decision question: should MVP support only owner-operated businesses, or must it support multiple staff members from launch?

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

### Phase 0 — Current Foundation Stabilisation

**Goal:** Current repository is renamed, stable, and all tooling is green.

Tasks:
1. Verify the existing `application/account`, `application/main`, `application/back-office`, `shared-kernel`, and `shared-webapp` structure against this plan.
2. Complete any remaining global rename work: `PlatformPlatform` → `Nerova Bookings`, `platformplatform` → `nerovabookings`.
3. Remove or defer any planned `integrations/` SCS work from MVP setup.
4. Verify `developer-cli build` and `developer-cli test` pass clean.
5. Wire CI/CD — verify GitHub Actions pipeline green.

---

### Phase 1 — MVP Integration Boundary

**Goal:** Calendar and notification integrations are isolated behind internal interfaces without building the self-owned iPaaS yet.

**Calendar (Nango-first):**
- Add `CalendarConnection` aggregate in `main`.
- Store provider, Nango connection ID, selected calendar ID, status, last sync timestamp, and last error.
- Add Nango auth-complete webhook handling.
- Add Nango sync webhook handling.
- Import external calendar events as busy-time blocks only.
- Availability queries consume imported busy blocks.
- No outbound writes to Google/Outlook in MVP unless explicitly approved.

**WhatsApp booking:**
- Keep WhatsApp behind an internal booking/messaging abstraction.
- Use environment-configured Content SIDs and Flow IDs.
- Handle inbound WhatsApp messages and Flow submissions in `main`.
- Do not build a connector dashboard for MVP.

**Deferred:**
- Apache Camel iPaaS.
- Java/Spring service.
- Connector registry.
- Credential rotation dashboard.
- Route health dashboard.
- External calendar event writes.
- Platform-hosted public web booking page.

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

#### 2.4 WhatsApp Booking Flow

**Backend:**
- WhatsApp webhook and Flow API endpoints — unauthenticated from the client perspective, but signature-verified and provider-authenticated.
- `GetVendorProfile` query — returns business name, active services, locations, timezone, and WhatsApp Flow metadata.
- `GetAvailableSlots` query — computes free slots from schedule, blocked times, existing appointments (respects buffer, `MinimumBookingNoticeMinutes`, `MaxAdvanceBookingDays`)
- `StartWhatsAppBookingConversation` command — starts or resumes the chatbot booking state for a client phone number.
- `HandleWhatsAppFlowSubmission` command — validates Flow payloads, creates/updates client records, checks slot availability, records opt-in, creates appointments, and starts payment where required.
- `CreateBooking` command — creates `Appointment` with `Source = WhatsAppFlow`; triggers confirmation in the same WhatsApp conversation.

**MVP dependency:** if PayFast is not implemented yet, paid-upfront services (`Price > 0` and `PaymentTiming = Before`) must not be offered inside the WhatsApp booking flow. The MVP release should include PayFast before enabling paid-upfront services for clients.

**Slot algorithm:**
1. Resolve tenant timezone and compute the requested local day with DST-safe boundaries.
2. Load `BusinessSchedule` and any location/staff overrides that apply.
3. Load all `Appointment`s that day (status != Cancelled) for the location/service/staff scope.
4. Load all `BlockedTime`s and imported external calendar busy blocks for the day.
5. Generate candidate start times using a configurable start interval. Do not assume interval = `DurationMinutes + BufferMinutes`.
6. For each candidate, compute occupied time using duration, buffer, extra time before, and extra time after.
7. Remove candidates that conflict with appointments, blocks, imported busy time, capacity limits, or booking-period rules.
8. Apply `MinimumBookingNoticeMinutes` from now.
9. Protect booking creation with a database-level conflict check or transactional uniqueness strategy so two clients cannot book the same slot concurrently.
10. Return available `DateTimeOffset` slots.

**Provider webhook/API endpoints:**
```
POST /api/main/whatsapp/inbound                  (Twilio/WhatsApp inbound webhook)
POST /api/main/whatsapp/flows/data               (WhatsApp Flow data exchange / dynamic options)
POST /api/main/whatsapp/flows/submit             (Flow submission handler)
POST /api/main/whatsapp/appointments/{managementToken}/cancel
POST /api/main/whatsapp/appointments/{managementToken}/reschedule
```

Appointment management must never authorize by `AppointmentId` alone. Use WhatsApp conversation identity plus a separate opaque management token, signed link, or OTP verification flow.

**Frontend:**
- No customer-facing web booking routes in MVP.
- Dashboard screens configure WhatsApp onboarding, allowed customization, service questions, and sender status.
- Any web management link is a fallback, not the primary booking surface.

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
- `BusinessProfile` aggregate (fields in Section 7, including WhatsApp identity and Flow configuration needed for MVP)
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
- `routes/dashboard/settings.booking.tsx` (WhatsApp booking customization — cancellation policy, branding colour, welcome message, service-specific questions)
- `routes/dashboard/settings.notifications.tsx` (notification preferences — which events trigger messages)

---

### Phase 4 — Calendar Integration (Nango MVP)

**MVP goal:** use Nango to let a tenant connect Google Calendar or Outlook and import busy time so WhatsApp booking avoids obvious conflicts. Do not build the self-owned iPaaS in MVP.

**Nango responsibilities:**
- OAuth connection flow for Google Calendar and Outlook.
- Token refresh.
- Provider proxying.
- Sync/webhook lifecycle events.

**main SCS additions:**

`CalendarConnection` aggregate:
- `TenantId`, `Provider` (Google/Outlook), `NangoConnectionId`, `ConnectedCalendarId`, `SyncEnabled`, `SyncDirection` (ImportOnly for MVP), `ConnectedAt`, `LastSyncAt`, `LastSyncError`, `Status`
- ID prefix: `calset`

`ExternalCalendarBusyBlock` aggregate:
- `TenantId`, `CalendarConnectionId`, `ExternalEventId`, `StartsAt`, `EndsAt`, `TitleHash?`, `IsDeleted`, `LastSeenAt`
- Stores only what is needed for availability. Avoid storing unnecessary external event details in MVP.

`CalendarSync` feature:
- `HandleNangoAuthWebhook` command — reconciles Nango connection IDs to tenant connections
- `HandleNangoSyncWebhook` command — imports changed busy blocks from Nango
- `GetCalendarSyncStatus` query
- `DisconnectCalendar` command — disables sync and removes/revokes the Nango connection where supported

API:
```
GET    /api/main/calendar-connections
POST   /api/main/calendar-connections/{provider}/connect    (initiates Nango OAuth flow)
POST   /api/main/calendar-connections/nango/webhook         (Nango → main, signature-verified)
DELETE /api/main/calendar-connections/{provider}            (disconnect)
```

Frontend:
- `routes/dashboard/settings.calendar.tsx` — connect/disconnect Google + Outlook, sync status, last error

Post-MVP:
- Outbound writes of Nerova appointments to external calendars.
- True two-way sync.
- Conflict-resolution UI.
- Provider-specific webhook subscriptions outside Nango.

---

### Phase 5 — PayFast Payments

> **Note:** PayFast is called directly from the `main` SCS — it is not routed through iPaaS Camel.
> PayFast is exclusive to this platform; no multi-tenant connector management is needed.

**main SCS:**

`Payment` aggregate:
- `TenantId`, `AppointmentId`, `Amount`, `Currency`, `PayFastPaymentId` (their `pf_payment_id`), `MerchantPaymentId` (our generated ID), `Status` (Pending/Captured/Failed/Refunded), `CapturedAt`, `RefundedAt`, `RefundReason`, `PayFastRawResponse` (JSONB)
- ID prefix: `pay`

`HandlePayFastItn` command — receives ITN from PayFast directly, verifies signature, updates `Payment` and transitions `Appointment` to `Confirmed` (or `Cancelled` on failure)

`InitiatePayment` command — generates `MerchantPaymentId`, builds PayFast payment URL, returns redirect URL

`RefundPayment` command — calls PayFast refund API directly from `main` Core

**PayFast integration specifics:**
- Sandbox URL: `https://sandbox.payfast.co.za/eng/process`
- Live URL: `https://www.payfast.co.za/eng/process`
- Merchant ID + Merchant Key + Passphrase stored in AppHost secrets (same pattern as `account` SCS)
- ITN webhook: `POST /api/main/payments/payfast/itn` (no auth, signature-verified using MD5 + passphrase)
- Payment required if `ServiceType.Price > 0` and `ServiceType.PaymentTiming = Before`
- Booking flow: WhatsApp Flow service/slot/client details → PayFast payment link or redirect handoff → ITN → WhatsApp confirmation

**API endpoints:**
```
POST /api/main/payments/initiate             (creates payment intent, returns redirect URL)
POST /api/main/payments/payfast/itn          (PayFast ITN webhook — no auth, signature-verified)
POST /api/main/payments/{id}/refund
GET  /api/main/payments                      (list for dashboard)
GET  /api/main/payments/{id}
```

**Frontend:**
- Optional fallback `routes/payment-callback.tsx` (return URL from PayFast — show loading, poll for payment status, instruct client to return to WhatsApp)
- `routes/dashboard/payments.tsx` (transaction list, filters, refund actions)
- Payment step injected into WhatsApp booking flow when `Price > 0`

---

### MVP-Critical — WhatsApp Chatbot, Flows, and Notifications

This is not a late-stage notification feature. It is the primary booking surface and must be built with the core booking engine before MVP launch.

**Non-negotiable rules:**
1. **Never send free-text** to a client outside a 24-hour inbound session window. Always use `ContentSid`.
2. **Opt-in required.** No opt-in record → log + return (no throw, no silent drop).
3. **Single aggregate** — no dual source-of-truth. `WhatsAppOnboarding` on `BusinessProfile` (not a separate `WhatsAppSettings` table).
4. **Platform-owned templates v1** — 4 templates submitted once on Nerova Bookings' WABA, SIDs configured per environment.

**MVP Twilio WhatsApp booking service:**

`TwilioWhatsAppBookingService`:
- `SendWhatsAppFlow` message processor:
  1. Select the correct tenant Flow ID and Content SID.
  2. Populate Flow variables from business profile, service catalog, and appointment context.
  3. Send via the WhatsApp provider.
- `SendWhatsAppMessage` message processor:
  1. Load opt-in from `POST /api/main/whatsapp/opt-in/check`
  2. If opted-in AND `now - LastInboundAt < 24h` → send free-text body
  3. Otherwise → send with `ContentSid` + `ContentVariables`
  4. Idempotency key: `tenant-{id}-appt-{id}-{eventType}`
- `HandleInboundMessage` — receives Twilio webhook, updates `LastInboundAt`, creates `WhatsAppMessageLog`, and starts/resumes booking conversations.
- `HandleFlowSubmission` — receives WhatsApp Flow payloads and dispatches `HandleWhatsAppFlowSubmission`.
- Webhook endpoint: `POST /api/main/whatsapp/inbound` (Twilio signature verified)
- Polly retry: 3 attempts, exponential backoff, Twilio 429 → circuit breaker

Post-MVP, this booking service can move behind the self-built iPaaS if WhatsApp operations become more complex.

**main SCS:**

`WhatsAppOptIn` aggregate:
- `TenantId`, `ClientPhone` (normalised), `ConsentedAt`, `ConsentSource` (WhatsAppFlow/Manual/Import), `ConsentPurpose`, `RevokedAt?`, `LastInboundAt?`
- ID prefix: `woptin`
- Repository method: `GetByPhoneAsync(TenantId, string phone)`

`WhatsAppOnboarding` — **on `BusinessProfile`** (not separate aggregate):
- Fields: `TwilioSubaccountSid`, `TwilioSubaccountAuthToken` (encrypted via `ITokenEncryptor`), `PhoneNumber`, `TwilioPhoneNumberSid`, `WabaId`, `SenderSid`, `OnboardingStatus` (NotStarted/NumberReserved/EmbeddedSignupCompleted/Active/Failed), `NumberLifecycleStatus` (Reserved/Active/Released), `DisplayName`

`WhatsAppMessageLog` aggregate:
- `TenantId`, `AppointmentId?`, `ClientPhone`, `MessageType` (Confirmation/Cancellation/Reschedule/Reminder/Custom), `Status` (Sent/Failed/Blocked), `BlockReason?` (NoOptIn/SessionExpired), `ContentSid?`, `TwilioMessageSid?`, `SentAt?`, `ErrorCode?`
- ID prefix: `wamsg`

`WhatsAppBookingSession` aggregate:
- `TenantId`, `ClientPhone`, `CurrentStep`, `SelectedServiceTypeId?`, `SelectedLocationId?`, `SelectedStartAt?`, `AppointmentId?`, `PaymentId?`, `Status` (Active/Completed/Expired/Abandoned), `StartedAt`, `LastInteractionAt`, `ExpiresAt`
- ID prefix: `wabks`

`WhatsAppFlowSubmission` aggregate:
- `TenantId`, `ClientPhone`, `FlowId`, `FlowToken`, `PayloadHash`, `Status` (Received/Processed/Rejected), `AppointmentId?`, `ReceivedAt`, `ProcessedAt?`, `Error?`
- ID prefix: `wafsub`

**4 Platform Templates (v1 — ContentSIDs configured per environment):**
| Template | Variables |
|----------|-----------|
| `nerovabookings_confirmation` | `{{business_name}}`, `{{service_name}}`, `{{date_time}}`, `{{reference}}` |
| `nerovabookings_cancellation` | `{{business_name}}`, `{{service_name}}`, `{{date_time}}`, `{{reference}}` |
| `nerovabookings_reschedule` | `{{business_name}}`, `{{service_name}}`, `{{old_date_time}}`, `{{new_date_time}}`, `{{reference}}` |
| `nerovabookings_reminder` | `{{business_name}}`, `{{service_name}}`, `{{date_time}}`, `{{reference}}`, `{{hours_until}}` |

**API endpoints:**
```
GET    /api/main/whatsapp/onboarding          (get current onboarding state)
POST   /api/main/whatsapp/senders/search      (list available Twilio numbers by country)
POST   /api/main/whatsapp/senders/reserve     (purchase Twilio number + create subaccount)
POST   /api/main/whatsapp/senders/complete    (post embedded-signup callback → register sender)
POST   /api/main/whatsapp/senders/check       (poll sender registration status)
DELETE /api/main/whatsapp/senders             (disconnect + release Twilio number)
POST   /api/main/whatsapp/opt-in/check        (internal — called by WhatsApp booking/messaging boundary)
POST   /api/main/whatsapp/opt-in/record       (record opt-in from WhatsApp Flow)
DELETE /api/main/whatsapp/opt-in/{phone}      (revoke opt-in)
POST   /api/main/whatsapp/flows/data          (dynamic Flow data endpoint)
POST   /api/main/whatsapp/flows/submit        (Flow submission endpoint)
POST   /api/main/whatsapp/test-send           (send test template to authenticated user's phone)
```

**Frontend:**
- `routes/dashboard/connectors.tsx` (layout)
- `routes/dashboard/connectors.whatsapp.tsx` — onboarding wizard + sender status + opt-in stats + Flow status
- `routes/dashboard/settings.booking.tsx` — WhatsApp Flow settings, approved customization, service-specific questions

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
- **Integration health** — MVP reads Nango/notification status from `main`; post-MVP can read `GET /api/integrations/connectors/{id}/health`
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

// Fallback web surfaces only; WhatsApp booking is handled by provider webhooks/Flow endpoints.
import { publicApi } from "@/shared/lib/api/publicClient";
const { data } = publicApi.useQuery("get", "/api/main/public/appointments/{managementToken}", {
  params: { path: { managementToken } }
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
| Java/Spring/Apache Camel iPaaS (post-MVP only) | `integrations-engineer` | `integrations-reviewer` |
| Playwright E2E tests | `qa-engineer` | `qa-reviewer` |

### Delegation Rules (both modes)
- One SCS per agent call — never split `main` backend + `integrations` Java in one task
- Always pass the relevant PLAN.md section as context in the task prompt
- Engineer → Reviewer pipeline is mandatory before marking a task done
- Java (post-MVP integrations SCS only): use `mvn verify -q` directly — developer-cli MCP does not yet cover Java builds
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

| Problem | Fix in current plan |
|---------|---------------------|
| `WhatsAppSettings` aggregate duplicated fields from `BusinessProfile` | Single `WhatsAppOnboarding` embedded in `BusinessProfile` |
| Three separate WhatsApp status enums with non-1:1 mappings | Single `OnboardingStatus` + `LifecycleStatus` per inventory entry |
| Twilio sends free-text `Body` to all appointments — rejected outside 24h window | Always use `ContentSid` for business-initiated messages |
| Zero opt-in / consent tracking | `WhatsAppOptIn` aggregate, POPIA-compliant |
| Third-party SDK details leaking into domain handlers | Use internal integration abstractions; MVP calendar implementation uses Nango behind that boundary |
| Paystack wired directly in `main` Core | PayFast called directly from `main` Core with ITN signature verification |
| No idempotency on Twilio or payment calls | Idempotency keys on all external calls |
| `DisconnectWhatsApp` didn't call Twilio — tenant kept paying | Disconnect releases Twilio number through the notification/integration boundary |
| WhatsApp "flows" UI accepted config but backend never read it at send time | WhatsApp Flows are core booking infrastructure; only expose customization that the backend uses at send and submission time |
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
| `TeamMember` | `tmem` |
| `CalendarConnection` | `calset` |
| `ExternalCalendarBusyBlock` | `ecblk` |
| `Payment` | `pay` |
| `WhatsAppOptIn` | `woptin` |
| `WhatsAppMessageLog` | `wamsg` |
| `WhatsAppBookingSession` | `wabks` |
| `WhatsAppFlowSubmission` | `wafsub` |
| `Waitlist` | `wait` |
| `ApiKey` | `apikey` |
| `WebhookSubscription` | `whksub` |
| `WebhookDelivery` | `whkdlv` |
| `ClientTag` | `ctag` |

---

## 14. Open Decisions (Resolve Before Building Each Phase)

1. **MVP scope:** Solo-only MVP, or staff/team scheduling from launch?
2. **MVP scope:** One default location, or full multi-location support from launch?
3. **MVP payments:** PayFast in the first public release, or unpaid bookings first with paid bookings disabled until PayFast is complete?
4. **MVP calendar:** Busy-time import only via Nango, or also create/update external calendar events from Nerova appointments?
5. **MVP WhatsApp sender:** Tenant-owned WhatsApp sender from day one, or platform-owned sender while onboarding is proven?
6. **MVP WhatsApp customization:** Which parts of the Flow are configurable: branding/tone only, service-specific questions, cancellation policy, custom fields, or all of these?
7. **MVP appointment management:** Entirely inside WhatsApp, or WhatsApp plus secure fallback management links for complex changes?
8. **MVP WhatsApp provider:** Twilio WhatsApp Flows for MVP, Meta Cloud API directly, or another BSP?
9. **MVP market:** South Africa only, or South Africa plus one secondary market?
10. **Phase 0:** Which Azure region for prod? (affects Aspire env vars and Key Vault name)
11. **Phase 4:** Nango webhook handling requires a publicly reachable endpoint in non-local environments. In local dev, decide whether to use `ngrok`, an Aspire tunnel, or manual sync testing.
12. **Phase 5:** PayFast does not support direct refund API in all scenarios — confirm refund flow with PayFast sandbox before building the command.
13. **MVP WhatsApp:** Platform WhatsApp templates and Flows must be approved before they can be used for production business-initiated journeys. Allow time for approval. Build the send path with template SIDs and Flow IDs as config values, not hard-coded strings, so they can be swapped after approval.
14. **Phase 7 (Webhooks):** Worker for delivery retries — extend existing `Workers` project in `main` SCS using the boilerplate's job runner pattern.

---

## 15. Agent File Setup (Critical — Do Before Phase 0)

The PlatformPlatform boilerplate ships `.claude/agents/*.md` as **Claude Code worker-host passthrough proxies** (`tools: mcp__developer-cli__start_worker_agent` only). These must be replaced with **real implementation agents** so both Copilot CLI and Claude Code can dispatch them directly.

### Why the Change

| Current (boilerplate) | Target (Nerova Bookings repository) |
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
You are a **backend engineer** in the Nerova Bookings project implementing vertical-slice features in .NET 10.

## Role
- Implement commands, queries, domain models, repositories, API endpoints, and xUnit tests
- One task should produce one coherent changeset. Code must compile, run, and pass tests.
- Build and test incrementally after each meaningful change, not only at the end
- When complete, delegate to `backend-reviewer`
- Never commit, amend, or revert unless the developer explicitly asks for that git action in the current conversation.

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
Call reviewer:
`task(agent_type="backend-reviewer", prompt="Review: [what was implemented] on branch [branch]")`

Only commit after explicit developer instruction. Commit messages must be one descriptive line in imperative form with no description body.
```

### frontend-engineer.md

```markdown
You are a **frontend engineer** in the Nerova Bookings project implementing React/TypeScript features.

## Role
- Implement TanStack Router routes, React components, API integration, and translations
- One task should produce one coherent changeset
- Test in browser via Playwright MCP — zero tolerance for visual regressions
- When complete, delegate to `frontend-reviewer`
- Never commit, amend, or revert unless the developer explicitly asks for that git action in the current conversation.

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
Call reviewer:
`task(agent_type="frontend-reviewer", prompt="Review: [what was implemented] on branch [branch]")`

Only commit after explicit developer instruction. Commit messages must be one descriptive line in imperative form with no description body.
```

### backend-reviewer.md

```markdown
You are a **backend reviewer** in the Nerova Bookings project. Review .NET backend code for correctness and convention compliance. Never implement — return feedback only.

## Review Checklist
- [ ] Aggregate: `AggregateRoot<T>`, private ctor, static `Create` factory, `IEntityTypeConfiguration` in same file
- [ ] ID: `StronglyTypedUlid` with correct `[IdPrefix("xxx")]` — check PLAN.md Section 13
- [ ] Repository: `ICrudRepository<T>` interface + `RepositoryBase` implementation in same file under `Domain/`
- [ ] Handler: never calls `SaveChanges()` directly (UnitOfWork pipeline handles it)
- [ ] Validator: covers all required fields with sensible max lengths
- [ ] API endpoint: correct HTTP method, registered in correct endpoint group, auth applied
- [ ] Tests: happy path + validation failure + not-found + wrong-tenant minimum
- [ ] No provider SDK details leak into domain handlers. Calendar uses the Nango-backed integration boundary in MVP; WhatsApp uses the booking/messaging boundary. PayFast is exempt and is called directly from `account` and `main` Core.
- [ ] No Google, Microsoft, or Twilio-specific logic inside appointment/service/client domain handlers.

## Output
Return exactly one of:
- `✅ APPROVED — [brief summary of what was implemented]`
- `❌ CHANGES REQUIRED — [specific issues with file:line references]`
```

### frontend-reviewer.md

```markdown
You are a **frontend reviewer** in the Nerova Bookings project. Review React/TypeScript code. Never implement — return feedback only.

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
You are a **QA engineer** in the Nerova Bookings project writing Playwright E2E tests.

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
You are a **QA reviewer** in the Nerova Bookings project. Review Playwright E2E tests for coverage and reliability. Never implement — return feedback only.

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

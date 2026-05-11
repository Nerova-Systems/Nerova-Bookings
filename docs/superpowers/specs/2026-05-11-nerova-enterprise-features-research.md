# Nerova Enterprise Features Research

Date: 2026-05-11

## Purpose

Nerova should become the tenant-facing commercial platform. Cal.diy should provide scheduling and integration reference material, not define the product architecture. This document captures the implementation patterns for the enterprise features Cal.com keeps in its commercial product and Cal.diy does not provide as production-ready features.

The goal is to build these capabilities in PlatformPlatform/Nerova first, then port or adapt Cal.diy scheduling and integration slices into that control plane.

## Architecture Position

- PlatformPlatform already owns tenant identity, account users, billing, subscriptions, telemetry, API conventions, and Azure-ready deployment structure.
- Nerova should own enterprise product concepts: teams, staff, permissions, workflows, routing, CRM, loyalty, analytics, WhatsApp, Meta, Twilio, Paystack, and integration runtime.
- Cal.diy should be treated as an MIT-licensed scheduling and integration reference. Its useful parts are app-store patterns, event types, availability, bookings, calendars, conferencing, Daily.co, app metadata, and API/library shapes.
- Cal.com commercial docs are product-requirement references only. Do not copy closed-source implementation, proprietary UI, or private enterprise behavior.
- Nango can be removed only if Nerova takes ownership of OAuth flows, encrypted credentials, provider webhooks, retries, idempotency, background sync, rate limits, and provider API changes.

## Current Source Findings

These findings were checked on 2026-05-11 and should guide the implementation direction.

- Cal.com API v2 still exposes useful product surfaces for reference: bookings, schedules, slots, calendars, conferencing, event types, routing forms, webhooks, teams, organization users, organization roles, and organization memberships.
- Cal.com API v2 documents three auth modes: OAuth, API key, and Platform. Its Platform offering is marked deprecated/maintenance for existing customers and not offered for new signups as of 2025-12-15. This supports avoiding a Cal.com SaaS dependency for Nerova's embedded product.
- Cal.com OAuth requires a reviewed OAuth client, registered redirect URIs, selected scopes, and user approval. It is a good integration model, but it does not satisfy Nerova's requirement to keep tenant administration inside one Nerova dashboard.
- Cal.com access control combines role hierarchy, optional PBAC, and OAuth scopes. This is a useful target shape for Nerova, but it should be implemented against PlatformPlatform tenants and `main` resources.
- Cal.com team event types expose `collective`, `roundRobin`, and `managed` scheduling types, host lists, `assignAllTeamMembers`, buffers, booking limits, seats, locations, and email settings. This validates the service/team/staff model below.
- Cal.com event type location docs state that setting a conferencing app location does not install the app, and only a limited set of conferencing apps can be installed through the API. Other apps require Cal.com web app connection. This is direct evidence that Nerova needs its own integration installation/runtime if tenants must stay inside Nerova.
- Google Calendar synchronization relies on initial full sync, persisted sync tokens, repeated incremental sync, and full resync when tokens are invalidated.
- Microsoft Graph synchronization uses delta query for pull-based change tracking and change notifications for push-based alerts. Notification subscriptions have expiry limits, so renewal must be part of the integration runtime.
- Twilio messaging relies on inbound message webhooks and outbound status callbacks. Webhook payloads can evolve, so handlers must validate signatures and tolerate additional fields.
- Paystack payment request and transaction flows can be modeled as backend-created payment intents with provider references, verification, and webhook reconciliation.
- Daily.co rooms can be created by API with privacy, expiry, max participant, recording, transcription, and webhook-related options. Cal.diy's Daily.co adapter is a useful porting reference.
- OWASP logging guidance supports keeping audit trails separate from operational telemetry and excluding secrets, tokens, payment card data, and unnecessary PII from logs.

## Existing PlatformPlatform Foundation

- `account` already provides tenants, users, owner/admin/member roles, invitations, tenant switching, tenant profile/logo, tenant state, and subscription plan state.
- `account` already owns Paystack subscription billing, webhooks, payment attempts, upgrades, downgrades, cancellations, and scheduled downgrades.
- `account` has email OTP login, Google OAuth login, sessions, refresh tokens, and external authentication flow structure.
- `BackOffice` provides a separate administrative surface authenticated through Entra/EasyAuth in production and MockEasyAuth locally.
- `SharedKernel` provides strong IDs, tenant execution context, soft delete, domain events, CQRS/MediatR, validation, OpenAPI generation, telemetry, and persistence conventions.
- `main` is mostly open product space and should host Nerova's commercial platform features before deep Cal.diy porting.

## Feature Guidelines

### 1. PBAC and Resource Authorization

Recommended pattern: RBAC for role assignment, PBAC for fine-grained capability checks, and resource-based authorization for tenant/team/staff-owned objects.

Implementation guideline:
- Keep `Owner`, `Admin`, and `Member` as simple tenant roles.
- Add permission strings in the shape `resource.action`, for example `booking.read`, `workflow.update`, `integration.connect`, `staff.manage`, `analytics.read`.
- Store role templates per tenant plan, then allow future custom roles only after the base role model stabilizes.
- Evaluate coarse endpoint access first, then resource-specific access after loading the target entity.
- Default deny for unknown permissions, unknown resources, suspended tenants, and cross-tenant records.
- Audit every permission-sensitive mutation.

First version:
- Static permission registry in code.
- Owner gets all tenant permissions.
- Admin gets operational permissions except billing and destructive tenant actions.
- Member gets self/staff-scoped scheduling permissions.

References:
- ASP.NET Core policy authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies
- ASP.NET Core resource authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resourcebased
- NIST RBAC model: https://www.nist.gov/publications/nist-model-role-based-access-control-towards-unified-standard
- Cal.com API access control and PBAC concepts: https://cal.com/docs/api-reference/v2/access-control

### 2. Organizations, Teams, and Staff

Recommended pattern: treat the PlatformPlatform `Tenant` as the organization. Add teams and staff in `main`, not `account`, unless they affect authentication or billing.

Implementation guideline:
- `Tenant` remains the commercial account boundary.
- `StaffProfile` represents a bookable person. It may map to an account user, but it must also support non-login staff later.
- `Team` groups staff for scheduling, reporting, and routing.
- `TeamMembership` links staff to teams with operational role metadata.
- `ServiceAssignment` links services to eligible staff or teams.
- Staff should have profile fields needed for booking pages and WhatsApp flows: display name, timezone, phone, avatar, active state, service eligibility, and default location.

First version:
- Solo-first staff model with one owner-backed staff profile created automatically at tenant onboarding.
- Add team table shape early, but keep UI hidden until multi-staff is needed.

References:
- Cal.com Organizations and Teams API index: https://cal.com/docs
- Cal.com team event types: https://cal.com/docs/api-reference/v2/orgs-teams-event-types/create-an-event-type
- Calendly multi-person scheduling concepts: https://calendly.com/help/multi-person-scheduling-options-for-your-organization

### 3. Team Scheduling

Recommended pattern: model availability aggregation separately from host assignment.

Implementation guideline:
- Collective scheduling returns slots where all required hosts are available.
- Round-robin scheduling returns the union of eligible host availability, then assigns a host when the booking is created.
- Assignment should support priority, weight, least-recently-booked, and fixed hosts.
- Store an assignment ledger so decisions are explainable and stable under retries.
- Never make host selection depend only on current query order or random database ordering.

First version:
- Solo availability and booking only.
- Data model should not block round-robin later.
- Add assignment ledger when the second staff member is supported.

References:
- Cal.com round robin: https://cal.com/help/event-types/round-robin
- Cal.com collective events: https://cal.com/help/event-types/collective-events
- Calendly round robin distribution: https://help.calendly.com/hc/en-us/articles/4402432846999-Round-robin-distribution-overview

### 4. Managed Service Templates

Recommended pattern: parent template plus child override model.

Implementation guideline:
- A `ServiceTemplate` defines tenant-standard service settings: title, duration, description, buffers, minimum notice, location options, payment policy, intake form, and notification policy.
- A `StaffService` or `TeamService` derives from the template.
- Each field should have an override policy: locked, inherited, or overridden.
- Avoid cloning templates into unrelated records without a parent link, because future template updates become untraceable.

First version:
- Tenant services are the template and the active bookable service at the same time.
- Add explicit field-level override metadata only when multi-staff or managed service rollout starts.

References:
- Cal.com managed event type shape in API docs: https://cal.com/docs/api-reference/v2/orgs-teams-event-types/create-an-event-type
- Calendly managed events concept: https://help.calendly.com/hc/en-us/articles/23343597955863-Using-Calendly-with-a-team

### 5. Routing and Intake Forms

Recommended pattern: versioned forms, immutable responses, deterministic routing rules.

Implementation guideline:
- Store form definitions as versioned schemas.
- Store each submitted response against the exact form version used.
- Routing rules should produce a route result: selected service, selected staff/team, rejected/no-route state, and explanation.
- Route evaluation should be pure and replayable from stored response data.
- Do not mutate historical responses when forms or rules change.

First version:
- Intake fields attached to services.
- Simple routing from WhatsApp answers to a service.
- Keep generic form/rule entities behind the scenes for later UI.

References:
- Cal.com routing forms API: https://cal.com/docs/api-reference/v2/orgs-routing-forms/get-organization-routing-forms
- Calendly routing forms: https://help.calendly.com/hc/en-us/articles/4418606043927-How-to-create-a-Routing-Form

### 6. Workflow Engine

Recommended pattern: event-driven automation using transactional outbox, idempotent handlers, and durable scheduled jobs.

Implementation guideline:
- Use domain events and an outbox table for all workflow triggers.
- Workflow triggers: booking created, booking rescheduled, booking cancelled, booking completed, no-show marked, form submitted, payment succeeded, payment failed, client created, loyalty threshold reached.
- Workflow steps: send WhatsApp, send email, create Paystack payment link, apply loyalty reward, call webhook, update client status, create internal task.
- Every step execution needs idempotency key, attempt count, status, next retry time, and last error.
- Time-based steps should be scheduled as durable jobs, not in-memory timers.

First version:
- Hard-coded workflow templates for booking confirmation, reminder, follow-up, no-show recovery, and payment request.
- UI can expose toggles before a full workflow builder exists.

References:
- Transactional outbox: https://learn.microsoft.com/en-us/azure/architecture/best-practices/transactional-outbox-cosmos
- Durable orchestrations: https://learn.microsoft.com/en-us/azure/durable-task/common/durable-task-orchestrations
- CloudEvents specification: https://github.com/cloudevents/spec
- Cal.com team workflows API: https://cal.com/docs/api-reference/v2/orgs-teams-workflows/create-organization-team-workflow-for-routing-forms

### 7. Integration Platform

Recommended pattern: first-party app registry plus tenant connections, encrypted credentials, webhook routing, and background jobs.

Implementation guideline:
- `AppDefinition` should be code-defined and generated by developer tooling.
- `TenantIntegrationConnection` should track installed state, owner, scopes, health, and plan availability.
- `IntegrationCredential` should store encrypted OAuth/API credentials with purpose-specific encryption and no direct UI exposure.
- `IntegrationWebhookEndpoint` should route inbound provider webhooks to app handlers.
- `IntegrationJob` should own sync/action retries and provider rate-limit handling.
- Provider actions must be idempotent and observable.
- Cal.diy app-store CLI should inform a `developer-cli` integration generator, but Nerova should not inherit all Cal.diy package assumptions.

First version:
- Native Twilio, Meta WhatsApp, Paystack, Google Calendar, Microsoft Calendar, Daily.co.
- App registry and CLI generator before a marketplace UI.

References:
- OAuth 2.0 Security BCP: https://www.ietf.org/rfc/rfc9700.html
- Cal.diy app-store folder: `cal.diy/packages/app-store`
- Cal.diy app-store CLI: `cal.diy/packages/app-store-cli`
- Cal.com API app/conferencing/calendar references: https://cal.com/docs
- Cal.com OAuth scopes: https://cal.com/docs/api-reference/v2/oauth
- Twilio messaging webhooks: https://www.twilio.com/docs/usage/webhooks/messaging-webhooks
- Paystack transaction API: https://paystack.com/docs/api/transaction/
- Daily.co create room API: https://docs.daily.co/reference/rest-api/rooms/create-room

### 8. Calendar Sync

Recommended pattern: provider-specific sync state with webhook push where available and full resync fallback.

Implementation guideline:
- Store provider account, calendar ID, selected-calendar state, sync token/delta link, webhook/channel ID, expiry, and last successful sync.
- Google Calendar should use initial full sync, then incremental sync tokens.
- Microsoft Graph should use delta query plus change notifications where appropriate.
- Provider webhook notifications should enqueue sync work, not perform full sync inline.
- Expired sync tokens must trigger full resync.
- Calendar sync failures should degrade availability conservatively and surface connection health to the tenant.

First version:
- One primary external calendar per staff profile.
- Busy-time reads only, then create/update/delete calendar event writes later.

References:
- Google Calendar incremental sync: https://developers.google.com/workspace/calendar/api/guides/sync
- Google Calendar push notifications: https://developers.google.com/workspace/calendar/api/guides/push
- Microsoft Graph delta query: https://learn.microsoft.com/en-gb/graph/delta-query-overview
- Microsoft Graph change notifications: https://learn.microsoft.com/en-us/graph/change-notifications-overview

### 9. Audit Logs

Recommended pattern: append-only audit records for security-sensitive and business-critical mutations.

Implementation guideline:
- Capture actor, tenant, action, target type, target ID, timestamp, outcome, correlation ID, IP, user agent, and a sanitized change summary.
- Use separate audit records from telemetry events. Telemetry is for product/ops analysis; audit is for accountability.
- Do not log access tokens, refresh tokens, API keys, payment card data, raw WhatsApp message bodies by default, or unnecessary PII.
- Make impersonation and back-office actions explicit in the actor model.

First version:
- Audit staff, user, role, service, availability, integration, workflow, payment-policy, and booking mutations.
- Provide back-office read view before tenant-facing export.

References:
- OWASP Logging Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html
- Microsoft Entra audit logs: https://learn.microsoft.com/en-us/entra/identity/monitoring-health/concept-audit-logs

### 10. SAML and SCIM

Recommended pattern: standards-based enterprise identity, deferred until multi-staff/team plans exist.

Implementation guideline:
- SAML should authenticate tenant users through the customer's identity provider.
- SCIM should provision and deprovision users and groups into Nerova.
- SCIM should map groups to Nerova role templates and team assignments.
- Treat SCIM updates as external administrative mutations and audit them.
- Do not mix SAML/SCIM into the solo-first scheduling slice.

First version:
- No implementation in solo launch.
- Reserve identity provider and external directory identifiers on user/team entities.

References:
- Microsoft Entra provisioning with SCIM: https://learn.microsoft.com/en-us/entra/identity/app-provisioning/how-provisioning-works
- SCIM RFC 7644: https://www.rfc-editor.org/rfc/rfc7644

### 11. Impersonation

Recommended pattern: break-glass support session with explicit reason, time-box, and audit trail.

Implementation guideline:
- Only back-office support/admin identities can start impersonation.
- Require reason, target tenant, target user, expiry, and visible in-app banner.
- Prevent viewing secrets and raw credentials during impersonation.
- Record every action under both support actor and impersonated user context.
- Prefer read-only impersonation first.

First version:
- Defer until support operations require it.
- Build audit log actor model now so impersonation fits later.

References:
- Microsoft Entra audit log actor/target concepts: https://learn.microsoft.com/en-us/entra/identity/monitoring-health/concept-audit-logs
- OWASP logging sensitive-data guidance: https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html

### 12. Insights and Analytics

Recommended pattern: event stream plus query-optimized read models.

Implementation guideline:
- Derive analytics from booking, payment, client, message, and workflow events.
- Do not calculate dashboards directly from transactional tables once volume grows.
- Track booking volume, cancellation rate, no-show rate, repeat clients, revenue, service performance, staff utilization, reminder conversion, and loyalty impact.
- Keep analytics dimensions tenant-scoped and privacy-conscious.

First version:
- Daily aggregates for bookings, revenue, cancellations, no-shows, repeat clients, and top services.
- Add cohort and retention analysis after CRM/client identity stabilizes.

References:
- CloudEvents for event metadata consistency: https://github.com/cloudevents/spec
- Microsoft transactional outbox guidance: https://learn.microsoft.com/en-us/azure/architecture/best-practices/transactional-outbox-cosmos
- Cal.diy feature matrix for missing Insights: `cal.diy/apps/docs/content/index.mdx`

## Build Order Recommendation

1. Permission registry and audit actor model.
2. Staff, teams, and service assignment model.
3. App registry, credentials, and integration connection model.
4. Booking/service/availability commercial model in `main`.
5. Workflow trigger and execution model.
6. Calendar sync model.
7. Routing/intake model.
8. Analytics event stream and first read models.
9. Cal.diy scheduling slice port.
10. Optional enterprise identity: SAML, SCIM, impersonation.

## Acceptance Criteria for the Research Phase

- Each feature has a chosen architectural pattern.
- Each feature has a clear owner: PlatformPlatform/Nerova, Cal.diy-derived scheduling, or external provider.
- Each feature has an initial version small enough to build before the full Cal.diy port.
- Each feature has security and audit expectations.
- The implementation plan can be split into independent specs without making Cal.diy the product authority.

## Assumptions

- Nerova is solo-business first, but the data model should not block teams and organizations.
- The one-dashboard product promise is non-negotiable.
- Paystack, Twilio, Meta WhatsApp, and client CRM are Nerova-owned integrations, not Cal.diy-owned apps.
- Cal.diy will be used for reference and selected porting, not exposed as a tenant dashboard.
- Nango removal is desirable, but only after Nerova has a secure integration runtime.
- Commercial Cal.com features should be replicated as Nerova-native capabilities where they support Nerova's product, not as a wholesale copy of Cal.com's UI or internal implementation.

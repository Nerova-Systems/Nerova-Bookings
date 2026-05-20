# Cal.com Core Connectors Wave 1.12 Manifest

## Scope

Wave 1.12 starts the real-provider adapter layer for the core connector set. The availability checkpoint replaced the fake-only free/busy seam with provider HTTP adapters. Wave 1.12b added deterministic provider write semantics for Google Calendar/Meet, Office 365 Calendar/video, and Zoom. Wave 1.12c adds direct provider OAuth credential creation and protected tenant-scoped token refresh while keeping live account E2E optional and provider subscription webhooks blocked.

## Runtime Targets

- `application/main/Core/Features/Connectors/Domain/CoreConnectorClient.cs`
- `application/main/Core/Features/Connectors/Domain/CoreConnectorProviders.cs`
- `application/main/Core/Features/Connectors/Domain/CoreConnectorOAuth.cs`
- `application/main/Core/Features/Connectors/Domain/ConnectorCredentialRepository.cs`
- `application/main/Core/Features/Connectors/Domain/ConnectorTokenSecret.cs`
- `application/main/Core/Features/Connectors/Commands/ManageCoreConnectorOAuth.cs`
- `application/main/Core/Features/Connectors/Queries/GetCoreConnectorAccounts.cs`
- `application/main/Api/Endpoints/CoreConnectorEndpoints.cs`
- `application/main/Core/Configuration.cs`
- `application/AppHost/Program.cs`
- `application/main/Core/Database/Migrations/20260520120000_AddConnectorTokenSecrets.cs`
- `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAdvancedTab.tsx`
- `application/main/Core/Features/Scheduling/Queries/GetPublicSlots.cs`
- `application/main/Core/Features/Scheduling/Commands/CreatePublicBooking.cs`
- `application/main/Core/Features/BookingSideEffects/Domain/BookingSideEffectEnqueueHandler.cs`
- `application/main/Core/Features/BookingSideEffects/Workers/BookingSideEffectProcessor.cs`

## Cal.com Source Rows Reviewed

- `packages/app-store/googlecalendar/lib/CalendarService.ts`
- `packages/app-store/googlecalendar/lib/__tests__/CalendarService.test.ts`
- `packages/app-store/googlecalendar/api/add.ts`
- `packages/app-store/googlecalendar/api/callback.ts`
- `packages/app-store/office365calendar/lib/CalendarService.ts`
- `packages/app-store/office365calendar/api/add.ts`
- `packages/app-store/office365calendar/api/callback.ts`
- `packages/app-store/zoomvideo/lib/VideoApiAdapter.ts`
- `packages/app-store/zoomvideo/lib/VideoApiAdapter.test.ts`
- `packages/app-store/zoomvideo/api/add.ts`
- `packages/app-store/zoomvideo/api/callback.ts`
- `packages/app-store/InstallAppButton.tsx`
- `packages/app-store/InstallAppButtonWithoutPlanCheck.tsx`
- `packages/app-store/_components/OmniInstallAppButton.tsx`
- `apps/web/components/getting-started/components/AppConnectionItem.tsx`
- `apps/web/components/getting-started/components/ConnectedCalendarItem.tsx`
- `apps/web/modules/calendars/components/AdditionalCalendarSelector.tsx`
- `apps/web/modules/calendars/components/SelectedCalendarsSettingsWebWrapper.tsx`
- `packages/features/bookingReference/repositories/BookingReferenceRepository.ts`
- `packages/features/bookingReference/repositories/BookingReferenceRepository.integration-test.ts`

## Status Notes

- Google Calendar selected-calendar freebusy behavior is adapted into `GoogleCalendarCoreConnectorProvider`.
- Office 365 Calendar selected-calendar `calendarView` batch behavior is adapted into `Office365CalendarCoreConnectorProvider`.
- Google Calendar event create/update/delete writes are adapted into `GoogleCalendarCoreConnectorProvider`, including Google Meet `conferenceData` when the event type default conferencing app is `google-meet`.
- Office 365 Calendar event create/update/delete writes are adapted into `Office365CalendarCoreConnectorProvider`, including Teams online meeting creation when the event type default conferencing app is `office365-video`.
- Zoom meeting create/update/delete writes are adapted into `ZoomCoreConnectorProvider`.
- The runtime now has availability, calendar-write, conferencing-write provider boundaries, a tenant-scoped composite `CoreConnectorClient`, and a protected `ICoreConnectorAccessTokenProvider` implementation that refreshes expired Google/Microsoft/Zoom tokens.
- Direct OAuth authorization and callback endpoints are adapted for Google Calendar, Office 365 Calendar, and Zoom using DataProtection-protected state and owner-scoped credential replacement.
- `connector_token_secrets` stores protected token JSON; `ConnectorCredential.SecretReference` points at the protected token record.
- Provider OAuth code paths include deterministic mock codes for backend and E2E proof without requiring live Google/Microsoft/Zoom accounts.
- The event type advanced tab now exposes connect and disconnect controls for the core connectors. Copied-before-adapted source for install/calendar UI rows is recorded under `application/main/WebApp/routes/-scheduling/calcom-port-source/core-connectors/wave-1-12c`.
- Core connector account responses now include configured/connected metadata for Google Calendar, Office 365 Calendar, and Zoom so the WebApp can show configuration-required states instead of broken connect actions.
- Aspire/AppHost now exposes separate connector OAuth app-key parameters for Google Calendar, Office 365 Calendar, and Zoom, distinct from login OAuth. Main API receives the public callback URL and all client ids/secrets; Main Workers receive ids/secrets for token refresh during side-effect processing.
- Existing deterministic fake fixtures remain active for E2E and booking side-effect proof.
- Booking side-effect payloads now attach Google Meet/Office 365 video to calendar deliveries when the conferencing app matches the destination calendar provider. Zoom remains a standalone conferencing delivery.
- Live provider account E2E, provider subscription webhooks, token revocation, credential refresh hardening beyond standard refresh-token exchange, and Azure Key Vault backing remain blocked for later connector hardening.

## Tests

- `application/main/Tests/Scheduling/CoreConnectorProviderClientTests.cs`
- `application/main/Tests/Scheduling/CoreConnectorEndpointsTests.cs`
- `application/main/Tests/Scheduling/PublicSchedulingEndpointsTests.cs`
- `application/main/Tests/Scheduling/BookingSideEffectsTests.cs`

## Verification

- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProviderClientTests" --quiet`
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.BookingSideEffectsTests|FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProviderClientTests" --quiet`
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.PublicSchedulingEndpointsTests" --no-build --quiet`
- `dotnet run --project developer-cli -- build --backend --quiet`
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProviderClientTests|FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.BookingSideEffectsTests|FullyQualifiedName~Main.Tests.Scheduling.PublicSchedulingEndpointsTests" --no-build --quiet`
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProviderClientTests|FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProductionEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.BookingSideEffectsTests|FullyQualifiedName~Main.Tests.Scheduling.PublicSchedulingEndpointsTests" --no-build --quiet`
- `dotnet run --project developer-cli -- build --frontend --quiet`
- `dotnet run --project developer-cli -- format --backend --frontend --no-build --quiet`
- `dotnet run --project developer-cli -- lint --backend --frontend --no-build --quiet`

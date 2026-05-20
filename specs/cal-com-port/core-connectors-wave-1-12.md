# Cal.com Core Connectors Wave 1.12 Manifest

## Scope

Wave 1.12 starts the real-provider adapter layer for the core connector set. The availability checkpoint replaced the fake-only free/busy seam with provider HTTP adapters. Wave 1.12b adds deterministic provider write semantics for Google Calendar/Meet, Office 365 Calendar/video, and Zoom while keeping OAuth credential creation and live provider accounts blocked.

## Runtime Targets

- `application/main/Core/Features/Connectors/Domain/CoreConnectorClient.cs`
- `application/main/Core/Features/Connectors/Domain/CoreConnectorProviders.cs`
- `application/main/Core/Features/Connectors/Domain/ConnectorCredentialRepository.cs`
- `application/main/Core/Configuration.cs`
- `application/main/Core/Features/Scheduling/Queries/GetPublicSlots.cs`
- `application/main/Core/Features/Scheduling/Commands/CreatePublicBooking.cs`
- `application/main/Core/Features/BookingSideEffects/Domain/BookingSideEffectEnqueueHandler.cs`
- `application/main/Core/Features/BookingSideEffects/Workers/BookingSideEffectProcessor.cs`

## Cal.com Source Rows Reviewed

- `packages/app-store/googlecalendar/lib/CalendarService.ts`
- `packages/app-store/googlecalendar/lib/__tests__/CalendarService.test.ts`
- `packages/app-store/office365calendar/lib/CalendarService.ts`
- `packages/app-store/zoomvideo/lib/VideoApiAdapter.ts`
- `packages/app-store/zoomvideo/lib/VideoApiAdapter.test.ts`
- `packages/features/bookingReference/repositories/BookingReferenceRepository.ts`
- `packages/features/bookingReference/repositories/BookingReferenceRepository.integration-test.ts`

## Status Notes

- Google Calendar selected-calendar freebusy behavior is adapted into `GoogleCalendarCoreConnectorProvider`.
- Office 365 Calendar selected-calendar `calendarView` batch behavior is adapted into `Office365CalendarCoreConnectorProvider`.
- Google Calendar event create/update/delete writes are adapted into `GoogleCalendarCoreConnectorProvider`, including Google Meet `conferenceData` when the event type default conferencing app is `google-meet`.
- Office 365 Calendar event create/update/delete writes are adapted into `Office365CalendarCoreConnectorProvider`, including Teams online meeting creation when the event type default conferencing app is `office365-video`.
- Zoom meeting create/update/delete writes are adapted into `ZoomCoreConnectorProvider`.
- The runtime now has availability, calendar-write, conferencing-write provider boundaries, a tenant-scoped composite `CoreConnectorClient`, and an `ICoreConnectorAccessTokenProvider` seam for the next OAuth/secret-storage slice.
- Existing deterministic fake fixtures remain active for E2E and booking side-effect proof.
- Booking side-effect payloads now attach Google Meet/Office 365 video to calendar deliveries when the conferencing app matches the destination calendar provider. Zoom remains a standalone conferencing delivery.
- Real OAuth callback flows, refresh-token persistence, provider credential creation UI, live provider accounts, token refresh, and calendar subscription webhooks remain blocked for the next provider-auth slice.

## Tests

- `application/main/Tests/Scheduling/CoreConnectorProviderClientTests.cs`
- `application/main/Tests/Scheduling/CoreConnectorEndpointsTests.cs`
- `application/main/Tests/Scheduling/PublicSchedulingEndpointsTests.cs`
- `application/main/Tests/Scheduling/BookingSideEffectsTests.cs`

## Verification

- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProviderClientTests" --quiet`
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.BookingSideEffectsTests|FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProviderClientTests" --quiet`
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.PublicSchedulingEndpointsTests" --no-build --quiet`

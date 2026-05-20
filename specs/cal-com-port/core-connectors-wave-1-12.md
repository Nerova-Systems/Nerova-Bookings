# Cal.com Core Connectors Wave 1.12 Manifest

## Scope

Wave 1.12 starts the real-provider adapter layer for the core connector set. This first checkpoint keeps OAuth credential creation and live provider accounts blocked, but replaces the fake-only availability seam with provider HTTP adapters that can be tested deterministically.

## Runtime Targets

- `application/main/Core/Features/Connectors/Domain/CoreConnectorClient.cs`
- `application/main/Core/Features/Connectors/Domain/CoreConnectorProviders.cs`
- `application/main/Core/Features/Connectors/Domain/ConnectorCredentialRepository.cs`
- `application/main/Core/Configuration.cs`
- `application/main/Core/Features/Scheduling/Queries/GetPublicSlots.cs`
- `application/main/Core/Features/Scheduling/Commands/CreatePublicBooking.cs`

## Cal.com Source Rows Reviewed

- `packages/app-store/googlecalendar/lib/CalendarService.ts`
- `packages/app-store/googlecalendar/lib/__tests__/CalendarService.test.ts`
- `packages/app-store/office365calendar/lib/CalendarService.ts`

## Status Notes

- Google Calendar selected-calendar freebusy behavior is adapted into `GoogleCalendarCoreConnectorProvider`.
- Office 365 Calendar selected-calendar `calendarView` batch behavior is adapted into `Office365CalendarCoreConnectorProvider`.
- The runtime now has an `ICoreConnectorProvider` boundary, a tenant-scoped composite `CoreConnectorClient`, and an `ICoreConnectorAccessTokenProvider` seam for the next OAuth/secret-storage slice.
- Existing deterministic fake fixtures remain active for E2E and booking side-effect proof.
- Real OAuth callback flows, refresh-token persistence, provider credential creation, calendar event write HTTP calls, Google Meet/Office 365 video real writes, and Zoom real meeting HTTP remain blocked for the next provider-auth/write slice.

## Tests

- `application/main/Tests/Scheduling/CoreConnectorProviderClientTests.cs`
- `application/main/Tests/Scheduling/CoreConnectorEndpointsTests.cs`
- `application/main/Tests/Scheduling/PublicSchedulingEndpointsTests.cs`

## Verification

- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorProviderClientTests" --quiet`
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.PublicSchedulingEndpointsTests" --no-build --quiet`

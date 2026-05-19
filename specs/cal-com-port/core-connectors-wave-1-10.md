# Cal.com Runtime Port Wave 1.10: Core Connector Management APIs

Baseline commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`

Source of truth: imported Cal.com source under `application/main/CalCom`.

## Scope

Only Google Calendar/Google Meet, Office 365 Calendar/Office 365 video, and Zoom are active in this wave.

## Runtime Added

- `application/main/Core/Features/Connectors/Domain/ConnectorCredential.cs`
  - Tenant/user-owned connected account record.
  - Stores provider identity, account email/display name, status, secret reference, and cached calendar list.
- `application/main/Core/Database/Migrations/20260519133000_AddCoreConnectorCredentials.cs`
  - Adds `connector_credentials`.
- `application/main/Core/Features/Connectors/Queries/GetCoreConnectorAccounts.cs`
  - Lists only Google Calendar, Office 365 Calendar, and Zoom credentials.
- `application/main/Core/Features/Connectors/Commands/UpdateEventTypeConnectorSettings.cs`
  - Updates selected calendars, destination calendar, and default conferencing app.
  - Validates credential ownership, provider compatibility, and calendar id membership.
- `application/main/Api/Endpoints/CoreConnectorEndpoints.cs`
  - `GET /api/connectors/core/accounts`
  - `PUT /api/event-types/{eventTypeId}/connector-settings/selected-calendars`
  - `PUT /api/event-types/{eventTypeId}/connector-settings/destination-calendar`
  - `PUT /api/event-types/{eventTypeId}/connector-settings/default-conferencing`
- `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAdvancedTab.tsx`
  - Uses connected account data instead of manual credential id entry.
  - Allows selecting destination calendar, selected calendars, and default conferencing from connected core accounts.

## Still Blocked

- Real OAuth add/callback flows for Google, Microsoft, and Zoom.
- Secret material storage and refresh-token management until PlatformPlatform secret-storage mapping is explicit.
- Provider HTTP calls against Google Calendar, Microsoft Graph, and Zoom APIs.
- Account-level Cal.com settings pages beyond copied source evidence.
- External Cal.com v1/v2 API compatibility.

## Test Evidence

- `application/main/Tests/Scheduling/CoreConnectorEndpointsTests.cs`
  - Lists only Google/Microsoft/Zoom core accounts.
  - Rejects unauthorized/non-owner access.
  - Persists selected calendars, destination calendar, and default conferencing.
  - Rejects missing/non-owned credentials.
- `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.CoreConnectorEndpointsTests" --quiet`
  - Result: 4 passed.
- `dotnet run --project developer-cli -- build --frontend --quiet`
  - Result: passed.

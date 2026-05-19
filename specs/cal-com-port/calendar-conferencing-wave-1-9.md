# Cal.com Runtime Port Wave 1.9: Core Calendar And Conferencing Connectors

Baseline commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`

Source of truth: imported Cal.com source under `application/main/CalCom`.

## Inventory Start

Initial ledger slice count: core connector rows are still mostly `imported`; non-core connector rows remain imported but out of runtime scope for this wave.

| Group | Rows | Status |
| --- | ---: | --- |
| `apps/api/v1/pages/api/selected-calendars` | 8 | partially adapted |
| `apps/api/v1/pages/api/destination-calendars` | 8 | partially adapted |
| `apps/api/v1/pages/api/connected-calendars` | 2 | imported |
| `apps/api/v2/src/modules/cal-unified-calendars` | 21 | partially adapted |
| `apps/api/v2/src/modules/destination-calendars` | 7 | partially adapted |
| `apps/api/v2/src/modules/conferencing` | 12 | partially adapted |
| calendar settings UI pages | 2 | imported |
| conferencing settings UI pages | 2 | imported |
| `packages/app-store/googlecalendar` | 24 | partially adapted |
| `packages/app-store/office365calendar` | 17 | partially adapted |
| `packages/app-store/zoomvideo` | 20 | partially adapted |

## Explicitly Out Of Scope For Wave 1.9

| Group | Rows | Status |
| --- | ---: | --- |
| `packages/app-store/dailyvideo` | 15 | deferred-for-pruning |
| `packages/features/calendar-subscription` | 23 | deferred-for-pruning |
| Apple, CalDAV, Exchange, ICS, Daily/Cal Video, app marketplace breadth | n/a | deferred-for-pruning |
| Teams/org routing, payments, external API v1/v2 breadth | n/a | blocked for later waves |

## Runtime Targets

- Backend runtime: `application/main/Core`, `application/main/Api`, and `application/main/Workers`.
- Frontend runtime: `application/main/WebApp`; any in-scope Cal.com frontend source must be copied first before adaptation.
- Protected PlatformPlatform foundations: auth, tenant ownership, secret storage, feature flags, OpenAPI generation, and deployment tooling.

## Copy-First UI Evidence

Copied Cal.com source files for connector settings into:

`application/main/WebApp/routes/-scheduling/calcom-port-source/core-connectors/wave-1-9/`

- `apps/web/components/apps/installation/EventTypeConferencingAppSettings.tsx`
- `apps/web/components/apps/DestinationCalendarSettingsWebWrapper.tsx`
- `apps/web/modules/calendars/components/SelectedCalendarsSettingsWebWrapper.tsx`
- `apps/web/modules/calendars/components/CalendarSwitch.tsx`
- `apps/web/modules/calendars/components/AdditionalCalendarSelector.tsx`
- `apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/calendars/page.tsx`
- `apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/conferencing/page.tsx`
- `packages/features/calendars/components/DestinationCalendarSelector.tsx`

## First Implementation Slice

- Add connector domain abstractions and fake/test connector clients before real third-party calls. Completed backend target: `application/main/Core/Features/Connectors/Domain/CoreConnectorClient.cs`.
- Persist selected calendars, destination calendar, and conferencing defaults in `EventTypeSettings`; persist booking reference sync metadata in `Booking.ReferencesJson`.
- Make public slot calculation merge selected Google/Microsoft calendar busy windows. Completed via `PublicSlotCalculator`, `GetPublicSlotsHandler`, and `CreatePublicBookingHandler`.
- Make booking create/cancel/reschedule/edit-location/add-guests enqueue idempotent calendar/conferencing side effects. Completed first pass via `BookingSideEffectEnqueueHandler` and `BookingSideEffectProcessor`.
- Adapt first connector settings controls into the event type advanced tab using shared primitives: destination calendar, visible selected calendars, and default conferencing app. Completed via `EventTypeAdvancedTab.tsx` and `schedulingTypes.ts`.
- Keep real OAuth credential flows ledgered as platform-replaced/blocked until mapped to PlatformPlatform auth and tenant secret storage.

## Deferred

- Real Google/Microsoft/Zoom OAuth and token refresh: blocked until PlatformPlatform credential and secret-storage mapping is implemented.
- Calendar subscription webhooks and cache refresh workers beyond first selected-calendar busy reads.
- Full Cal.com v1/v2 external API compatibility breadth.
- Teams/org routing, round-robin, payments, app-store marketplace breadth, SMS/WhatsApp, and recurring-chain edge cases.

## Test Evidence

- `Main.Tests.Scheduling.PublicSchedulingEndpointsTests.GetPublicSlots_WhenGoogleSelectedCalendarHasBusyWindow_ShouldRemoveBusySlot`
- `Main.Tests.Scheduling.BookingSideEffectsTests.CreatePublicBooking_WhenDestinationCalendarAndZoomLocationAreConfigured_ShouldSyncCalendarAndConferenceReferences`
- Verification run: `dotnet run --project developer-cli -- test --filter "FullyQualifiedName~Main.Tests.Scheduling.PublicSchedulingEndpointsTests|FullyQualifiedName~Main.Tests.Scheduling.BookingSideEffectsTests" --quiet`
- Verification result: 32 tests passed.
- Backend build: `dotnet run --project developer-cli -- build --backend --self-contained-system main --quiet`
- Frontend build: `dotnet run --project developer-cli -- build --frontend --quiet`
- Frontend lint attempt: `dotnet run --project developer-cli -- lint --frontend --self-contained-system main --no-build --quiet` failed because oxlint panicked with `Insufficient memory to create fixed-size allocator pool` across multiple packages, including unchanged shared packages. No connector-specific lint findings were reported before the tool crash.

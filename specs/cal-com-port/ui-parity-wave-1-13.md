# Cal.com UI Port Wave 1.13

## Baseline

- Cal.com commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`
- Runtime target: `application/main/WebApp`
- Reference target: imported Cal.com source under `application/main/CalCom`
- Parity target: event type editor, public booker, and authenticated bookings dashboard layout/state parity against the imported commit.

## Copy-First Evidence

Exact copied source evidence for this wave was created before runtime adaptation:

- Event type editor: `application/main/WebApp/routes/-scheduling/calcom-port-source/ui-parity/wave-1-13/event-type-editor`
- Public booker: `application/main/WebApp/routes/-scheduling/calcom-port-source/ui-parity/wave-1-13/public-booker`
- Booking dashboard: `application/main/WebApp/routes/-bookings/calcom-port-source/ui-parity/wave-1-13`

The Wave 1.13 source folders intentionally remain non-runtime evidence. Production changes stay in the normal route/component files.

## Implemented Slice

### Event Type Editor Shell

- Replaced the event type detail page's `SchedulingPageShell` composition with a Cal.com-shaped page header inside `AppLayout`.
- Moved the event type action cluster back into the page heading for desktop and mobile.
- Added the Cal.com-style mobile overflow action menu for preview, copy, embed placeholder, hide/show, and delete actions.
- Added the desktop embed placeholder to keep the Cal.com action cluster shape while real embed behavior remains later-wave scope.
- Removed the separate mobile hidden switch card; visibility is now controlled by desktop header switch or mobile action overflow.
- Reworked editor tabs toward Cal.com's desktop vertical nav and mobile horizontal nav/content frame behavior.

## Test Evidence

- Added mobile event type editor parity assertion for the overflow action menu.
- Added deterministic connector account fixtures to the visual harness so the advanced tab does not depend on live/authenticated connector APIs.
- `dotnet run --project developer-cli -- build --frontend --quiet` passed.
- `dotnet run --project developer-cli -- e2e "cal-com-ui-parity" --quiet --stop-on-first-failure` passed: `69 passed`.

## Still Open In Wave 1.13

- Convert visual harness from capture-only to screenshot reference comparison.
- Continue production parity reset for booking dashboard data-table/store/details behavior.
- Continue public booker store/mobile modal/StickyBox/skip-confirm parity.
- Replace the embed placeholder with the adapted Cal.com embed dialog once the embed slice is in scope.

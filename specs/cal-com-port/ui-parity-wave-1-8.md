# Cal.com UI Parity Wave 1.8: Booking Dashboard

Baseline commit: `cf2a55c42363ab79982eef11610e1de8151b45ce`

Source of truth: imported Cal.com source under `application/main/CalCom`.

Copied source evidence:

- Target copy area: `application/main/WebApp/routes/-bookings/calcom-port-source/ui-parity/wave-1-8/`
- Copied rows: 37 booking dashboard rows from `apps/web/modules/bookings`
- Copy discipline: exact source copied with `.source` suffix before runtime adaptation
- Ledger rows updated in `specs/cal-com-port/import-ledger.csv`

Runtime adaptation targets:

- `application/main/WebApp/routes/bookings/$status.tsx`
- `application/main/WebApp/routes/-bookings/BookingListContainer.tsx`
- `application/main/WebApp/routes/-bookings/BookingsList.tsx`
- `application/main/WebApp/routes/-bookings/BookingListItem.tsx`
- `application/main/WebApp/routes/-bookings/BookingCalendarContainer.tsx`
- `application/main/WebApp/routes/-bookings/BookingCalendarView.tsx`
- `application/main/WebApp/routes/-bookings/BookingDetailsSheet.tsx`
- `application/main/WebApp/routes/-bookings/BookingStatusTabs.tsx`
- `application/main/WebApp/routes/-bookings/BookingViewToggleButton.tsx`
- `application/main/WebApp/routes/-bookings/WeekPicker.tsx`

Visual evidence:

- Test path: `application/main/WebApp/tests/e2e/cal-com-ui-parity.spec.ts`
- Screenshot output folder: `cal-com-ui-parity/wave-1-8`
- Viewports: desktop `1440x900`, tablet `834x1112`, mobile `390x844`
- States: list upcoming, list unconfirmed, list past, list cancelled, list empty, list loading, list error, calendar week, calendar empty, calendar selected, details info, details actions, details reschedule

Adapted now:

- Cal.com-shaped status/filter/saved-segment/view-toggle control row
- Compact framed list rows with selected state and action dropdown placement
- Calendar frame, selected event state, empty/loading states, and event markers
- Larger details sheet with status badges, event metadata, attendees, responses, reschedule/cancel metadata, system id, join action, and action footer
- Deterministic visual parity route for booking dashboard states

Blocked/deferred:

- Cal.com data-table framework and faceted unique-value filters
- Sheet store navigation across list/calendar pages
- Keyboard next/previous/join shortcuts
- Booking audit/history segment
- Infinite query prefetch/probe navigation
- Calendar connector writes, payments, teams/org routing, and API v2 breadth

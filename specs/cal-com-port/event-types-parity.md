# Event Types Parity Ledger

Source policy: Cal.com is the reference implementation. Nerova owns runtime architecture, auth, tenant boundaries, billing, roles, and scheduling ownership rules.

Status values:

- `implemented`: behavior is represented, editable where relevant, persisted, returned, and tested in Nerova.
- `partial`: some behavior or data shape exists, but UI, validation, enforcement, or tests are incomplete.
- `missing`: Cal behavior is not represented yet.
- `blocked-by-downstream`: event-type UI must expose the seam, but full behavior needs another subsystem port.
- `not-applicable`: Cal behavior does not apply under a recorded Nerova product decision.

## Audit Snapshot

- Audit date: 2026-05-17
- Cal source root: `cal.com`
- Nerova target root: `application/main`
- Current implementation scope: authenticated event-type setup for owner-owned schedules plus solo public booker foundation.
- Current strongest parity area: basic event-type CRUD with schedule ownership and JSONB-backed settings.
- Current weakest parity area: dependency-heavy Cal editor tabs still need their owning downstream subsystems, and full booking lifecycle parity is not implemented yet.

## Cal References

| Area | Primary Cal.com references |
| --- | --- |
| Data model | `cal.com/packages/prisma/schema.prisma` `EventType`, `Host`, `HostGroup`, `HostLocation`, `EventTypeCustomInput`, `HashedLink`, `DestinationCalendar`, `SelectedCalendar`, `Webhook`, `Workflow*`, `AIPhoneCallConfiguration`, `CalVideoSettings`, `EventTypeTranslation` |
| API v2 | `cal.com/apps/api/v2/src/ee/event-types/event-types_2024_06_14/**` |
| API DTOs | `cal.com/packages/platform/types/event-types/event-types_2024_06_14/**` |
| List/create/duplicate/delete | `cal.com/apps/web/modules/event-types/views/event-types-listing-view.tsx`, `cal.com/apps/web/modules/event-types/components/CreateEventTypeDialog.tsx`, `DuplicateDialog.tsx`, `dialogs/DeleteDialog.tsx` |
| Editor shell | `cal.com/apps/web/modules/event-types/components/EventTypeLayout.tsx`, `EventType.tsx`, `EventTypeWebWrapper.tsx` |
| Setup | `cal.com/apps/web/modules/event-types/components/tabs/setup/EventSetupTab.tsx`, `cal.com/apps/web/modules/event-types/components/locations/**` |
| Availability | `cal.com/apps/web/modules/event-types/components/tabs/availability/EventAvailabilityTab.tsx` |
| Limits | `cal.com/packages/features/eventtypes/components/tabs/limits/EventLimitsTab.tsx`, `MaxActiveBookingsPerBookerController.tsx` |
| Advanced | `cal.com/apps/web/modules/event-types/components/tabs/advanced/EventAdvancedTab.tsx`, `FormBuilder.tsx`, `RequiresConfirmationController.tsx`, `DisableReschedulingController.tsx`, `MultiplePrivateLinksController.tsx` |
| Recurring | `cal.com/packages/features/eventtypes/components/tabs/recurring/EventRecurringTab.tsx`, `RecurringEventController.tsx` |
| Teams/hosts | `cal.com/apps/web/modules/event-types/components/tabs/assignment/EventTeamAssignmentTab.tsx`, `AddMembersWithSwitch.tsx`, `cal.com/packages/features/eventtypes/components/AssignAllTeamMembers.tsx`, `dialogs/HostEditDialogs.tsx` |
| Apps/conferencing | `cal.com/apps/web/modules/event-types/components/tabs/apps/EventAppsTab.tsx`, `locations/CalVideoSettings.tsx`, `locations/Locations.tsx` |
| Workflows | `cal.com/apps/web/modules/event-types/components/tabs/workflows/EventWorkflowsTab.tsx` |
| Webhooks | `cal.com/apps/web/modules/event-types/components/tabs/webhooks/EventWebhooksTab.tsx` |
| Instant | `cal.com/apps/web/modules/event-types/components/tabs/instant/EventInstantTab.tsx`, `InstantEventController.tsx` |
| AI | `cal.com/apps/web/modules/event-types/components/tabs/ai/EventAITab.tsx`, `AIEventController.tsx` |
| Public event helpers | `cal.com/packages/features/eventtypes/lib/getPublicEvent.ts`, `getEventTypesPublic.ts`, `getEventTypeById.ts`, `getEventTypesByViewer.ts` |
| Public booking page | `cal.com/apps/web/app/(booking-page-wrapper)/[user]/[type]/page.tsx`, `cal.com/apps/web/server/lib/[user]/[type]/getServerSideProps.ts` |
| Public booker UI | `cal.com/apps/web/modules/bookings/components/BookerWebWrapper.tsx`, `Booker.tsx`, `EventMeta.tsx`, `DatePicker.tsx`, `AvailableTimeSlots.tsx`, `AvailableTimes.tsx`, `BookEventForm/**` |
| Slot calculation | `cal.com/apps/web/modules/schedules/hooks/useSchedule.ts`, `useEvent.ts`, `cal.com/packages/trpc/server/routers/viewer/slots/**` |
| Authenticated bookings views | `cal.com/apps/web/app/(use-page-wrapper)/(main-nav)/bookings/[status]/page.tsx`, `cal.com/apps/web/modules/bookings/views/bookings-view.tsx`, `BookingListContainer.tsx`, `BookingList.tsx`, `BookingCalendarContainer.tsx`, `BookingCalendarView.tsx`, `LargeCalendar.tsx`, `WeekPicker.tsx`, `ViewToggleButton.tsx`, `cal.com/apps/web/components/booking/BookingListItem.tsx`, `BookingDetailsSheet.tsx` |
| Authenticated bookings API/filtering | `cal.com/packages/trpc/server/routers/viewer/bookings/get.handler.ts`, `get.schema.ts`, `find.handler.ts`, `cal.com/apps/web/modules/bookings/hooks/useBookingStatusTab.ts`, `useBookingFilters.ts`, `useBookingsView.ts`, `useBookingCalendarData.ts` |

## Parity Matrix

| Capability | Status | Nerova target files | Remaining implementation slice | Test owner |
| --- | --- | --- | --- | --- |
| Basic event-type create/list/get/update/delete | `implemented` | `application/main/Core/Features/EventTypes/**`, `application/main/Api/Endpoints/EventTypeEndpoints.cs`, `application/main/WebApp/routes/event-types/**` | Harden edge cases and E2E coverage | Backend + QA |
| Tenant/owner/schedule scoping | `implemented` | `EventTypeRepository.cs`, `CreateEventType.cs`, `UpdateEventType.cs`, `GetEventType.cs`, `GetEventTypes.cs` | Add cross-owner/cross-tenant tests | Backend |
| Owner/admin mutation permissions | `partial` | `SchedulingAuthorization.cs`, event-type commands | Add admin positive tests and read-policy decision | Backend |
| Slug uniqueness per owner | `implemented` | `EventTypeRepository.cs`, create/update handlers | Add soft-delete reuse test if required | Backend |
| List rows, New dialog, duplicate, delete, actions | `partial` | `application/main/WebApp/routes/event-types/index.tsx`, `routes/-scheduling/event-types-shell/**` | Add Cal grouping/profile/team seams, richer row metadata, E2E | Frontend + QA |
| Editor shell, header actions, search-param tabs | `partial` | `application/main/WebApp/routes/event-types/$eventTypeId.tsx`, `EventTypeEditorTabs.tsx` | Replace `JSON.stringify` dirty check, add mobile action parity, add E2E | Frontend + QA |
| Setup: title, slug, description, base duration | `implemented` | `EventTypeSetupTab.tsx`, event-type commands | Add Cal markdown/editor polish after base controls | Frontend |
| Setup: multiple durations | `partial` | `EventTypeSettings.cs`, `EventTypeSetupTab.tsx` | Expose and validate UI, enforce in booking/slots later | Frontend + Booking |
| Setup: locations array and event color | `partial` | `EventTypeSettings.cs`, `EventTypeSetupTab.tsx`, `EventTypeAdvancedTab.tsx` | Expose array controls and Cal-like location variants | Frontend |
| Setup: Cal Video settings | `blocked-by-downstream` | not present | Apps/conferencing slice | Apps |
| Availability: schedule selector | `implemented` | `EventTypeAvailabilityTab.tsx`, schedules API | E2E and schedule preview polish | Frontend + QA |
| Availability: restriction schedule and booker timezone | `missing` | event-type settings/API not complete | Availability parity slice | Backend + Frontend |
| Availability: per-host/team availability | `blocked-by-downstream` | not present | Teams/hosts slice | Teams |
| Limits: buffers, slot interval, minimum notice | `implemented` | event-type commands, `EventTypeLimitsTab.tsx`, `PublicSlotCalculator.cs` | Add E2E coverage | Backend + QA |
| Limits: future booking window | `partial` | `EventTypeSettings.BookingWindow`, `PublicSlotCalculator.cs` | UI controls and E2E coverage | Backend + Frontend |
| Limits: booking count/duration limits | `partial` | `EventTypeSettings.Limits` | UI controls now, full booking-limit enforcement later | Backend + Booking |
| Limits: first available slot, offset start, max active per booker | `partial` | `EventTypeSettings.Limits`, `PublicSlotCalculator.cs` | Max-active-per-booker enforcement after richer booking lifecycle | Backend + Frontend |
| Advanced: confirmation and email verification | `partial` | `EventTypeSettings.ConfirmationPolicy`, `CreatePublicBooking.cs` | Email verification and approval workflow later | Backend + Frontend + Booking |
| Advanced: custom booking fields | `partial` | `EventTypeSettings.BookingFields`, `CreatePublicBooking.cs`, public booker form | Cal-like form builder and richer field rendering | Frontend + Booking |
| Advanced: cancellation/reschedule policy | `partial` | `EventTypeSettings.CancellationPolicy`, `ReschedulePolicy` | UI controls now, booking lifecycle enforcement later | Backend + Frontend + Booking |
| Advanced: private links | `partial` | `EventTypeSettings.PrivateLinks`, `PublicSchedulingResolver.cs` | UI controls and first-class private-link API later | Backend + Frontend |
| Advanced: redirects, interface language, metadata | `partial` | `EventTypeSettings.Redirects`, `InterfaceLanguage`, `Metadata` | UI controls and semantic validation | Backend + Frontend |
| Advanced: destination/selected calendars | `blocked-by-downstream` | not present | Calendar sync slice | Calendar |
| Advanced: disable emails, hide calendar details, timezone lock, event name template | `missing` | not present | Advanced event settings slice | Backend + Frontend |
| Recurring events | `partial` | `EventTypeSettings.Recurrence` | UI controls now, booking-series enforcement later | Backend + Frontend + Booking |
| Seats | `partial` | `EventTypeSettings.Seats`, `PublicSlotCalculator.cs` | UI controls and richer attendee display later | Backend + Frontend + Booking |
| Teams/hosts, round-robin, managed events | `blocked-by-downstream` | not present | Teams/hosts parity slice | Teams |
| Apps/conferencing tab | `blocked-by-downstream` | not present | App-store/conferencing slice | Apps |
| Workflows tab | `blocked-by-downstream` | not present | Workflow slice | Workflows |
| Webhooks tab | `blocked-by-downstream` | not present | Webhook slice | Webhooks |
| Instant event tab | `blocked-by-downstream` | not present | Instant meeting slice | Backend + Frontend |
| AI tab | `blocked-by-downstream` | not present | AI phone slice | AI |
| Public handle and event preview URL | `implemented` | `SchedulingProfile.cs`, `PublicSchedulingEndpoints.cs`, event-type action helpers | Add profile management UI later | Backend + Frontend + QA |
| Public event resolution | `implemented` | `GetPublicEventType.cs`, `PublicSchedulingResolver.cs` | Add team/profile variants later | Backend |
| Public slot preview | `partial` | `GetPublicSlots.cs`, `PublicSlotCalculator.cs` | Add date override E2E, recurrence expansion, booking-limit enforcement | Backend + QA |
| Solo public booker UI | `partial` | `application/main/WebApp/routes/$handle/$eventSlug.tsx` | E2E and Browser visual parity pass | Frontend + QA |
| Minimal solo booking creation | `partial` | `Booking.cs`, `CreatePublicBooking.cs`, `/api/public/bookings` | Email, approval, reschedule/cancel, calendar/conferencing side effects later | Booking |
| Authenticated bookings status views, filters, and calendar view | `partial` | `application/main/Core/Features/Scheduling/Queries/GetBookings.cs`, `application/main/Api/Endpoints/BookingEndpoints.cs`, `application/main/WebApp/routes/bookings/**`, `routes/-bookings/**`, `application/main/WebApp/tests/e2e/bookings-flows.spec.ts` | Add booking actions, export, team/organization permissions, details audit, reschedule/cancel lifecycle, external calendar overlays | Booking + Frontend + QA |
| Full public booking lifecycle | `blocked-by-downstream` | minimal booking persistence only | Booking parity slice | Booking |
| E2E coverage for authenticated event-type setup | `implemented` | `application/main/WebApp/tests/e2e/event-types-flows.spec.ts` | Extend after public handle, slot preview, and dependency subsystems land | QA |
| Browser visual validation screenshots | `missing` | Browser plugin available | Final validation slice | QA |

## First Closure Waves

### Wave 1: Foundation Parity Hardening

- Harden `EventTypeSettings` semantic validation for existing persisted settings.
- Add create/update round-trip tests for every existing settings group.
- Expose existing persisted settings in Setup, Limits, Advanced, and Recurring tabs.
- Keep dependency tabs visible with explicit blocked states.
- Add this ledger as the issue gate for future event-type work.

### Wave 2: Public Handle And Slot Preview

- Status: partially implemented on 2026-05-17.
- Added owner public scheduling handle.
- Added public event resolution by handle plus event slug.
- Added slot preview endpoint that honors schedule windows, date overrides, buffers, slot interval, notice, duration, booking window, hidden/private-link visibility, timezone, first available slot, offset start, existing bookings, and seats where representable.
- Added minimal solo booking creation with slot recheck and required booking field validation.
- Remaining: full Cal recurrence expansion, booking count/duration limits, email verification, approval workflow, calendar/conferencing side effects, and E2E/browser validation.

### Wave 3: Booking-Dependent Event Settings

- Enforce booking fields, confirmation, email verification, cancellation/reschedule policy, redirects, seats, recurrence, max active bookings, count limits, and duration limits through booking creation/lifecycle.
- Port authenticated bookings management from Cal:
  - status tabs for upcoming, unconfirmed, recurring, past, and cancelled;
  - compact filter trigger with active count for event type, attendee name, attendee email, booking ID, and date range;
  - list/calendar view toggle with weekly calendar surface and week navigation;
  - booking details surface;
  - later actions for confirm/reject, cancel, reschedule, no-show, export, external calendar overlays, and audit.

### Wave 4: Dependency Tabs

- Teams/hosts and managed events.
- Calendar sync and destination/selected calendars.
- Apps/conferencing and Cal Video.
- Workflows.
- Webhooks.
- Instant meetings.
- AI phone.

### Wave 5: QA And Visual Parity

- Add E2E for public preview and slot preview after those APIs exist.
- Run Browser validation at `https://app.dev.localhost:9000` with `colinswart0@gmail.com` and OTP `UNLOCK`.
- Capture desktop and mobile screenshots for list, create dialog, each implemented tab, validation errors, duplicate, delete, and save flows.

## Current Test Gaps

- Unauthenticated event-type API tests.
- Admin positive permission tests.
- Cross-owner and cross-tenant read/update/delete/list tests.
- Base field boundary tests for slug, title, duration, buffers, slot interval, notice, and location lengths.
- Update settings invalid/round-trip tests.
- Public handle and slot-preview E2E tests.
- Browser validation screenshots for the public booker.
- Browser visual screenshots.

## Verification Gate

Required command order after implementation changes:

1. `dotnet run --project developer-cli -- build --quiet --backend --frontend --self-contained-system main`
2. In parallel with `--no-build`: `format`, `lint`, and `test`
3. Focused `e2e` for event-types after coverage exists.
4. Browser visual validation at `https://app.dev.localhost:9000`.

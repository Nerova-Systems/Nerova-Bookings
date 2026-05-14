# Cal.diy Scheduling Behavior Map

Date: 2026-05-11

Status: source-backed behavior map only. No production code changes.

Local source: `cal.diy`

## Purpose

This document maps how Cal.diy scheduling works across docs, REST API v2, web/tRPC, shared packages, and Prisma. It is the acceptance contract for the Nerova port.

The goal is not to port Cal.diy by directory. The goal is to copy the scheduling behavior that matters, simplify the behavior that is too broad for the first MVP, defer features that depend on later product decisions, and reject features that conflict with Nerova's one-dashboard platform model.

## Main Decision

The first PlatformPlatform slice after this map should be **Service/Event Types + Availability**.

Do not start with bookings. Booking correctness depends on event duration, slug routing, location, buffers, minimum notice, booking windows, active booking limits, selected calendars, destination calendars, public page behavior, private links, slot reservations, and cancellation or reschedule policy. Implementing bookings first would force Nerova to invent those rules before the model is stable.

Recommended sequence:

1. Service/Event Types + Availability schedules.
2. Selected calendars, destination calendars, and busy-time reads.
3. Slot calculation and temporary slot reservation.
4. Booking create/reschedule/cancel.
5. Confirmation, no-show, guests, attendees, webhooks, and video-side effects.

## Source Inventory

### Docs Intent

Primary docs source:

- `cal.diy/apps/docs/content/index.mdx`

The docs feature matrix says Cal.diy supports event types, recurring event types, seated events, paid events, private links, booking management, availability schedules, date overrides, buffer times, minimum notice, booking limits, travel schedules, out-of-office, calendar integrations, Cal Video/Daily.co, conferencing apps, webhooks, app integrations, embed, API v2, API keys, and platform/OAuth clients.

The same matrix says Cal.diy does not support Teams, team event types, managed event types, organizations, instant meetings, workflows, routing forms, SAML, SCIM, impersonation, insights, attributes/segments, delegation, workspace platform, or admin panel.

Important implication: the local source still contains many Cal.com team and organization seams, but the documented Cal.diy product surface is solo-first. Nerova should not blindly port team, round-robin, managed, or organization behavior until our own tenant model calls for it.

### REST API v2 Entry Points

API v2 is implemented in `cal.diy/apps/api/v2`.

Scheduling-relevant module wiring:

- `cal.diy/apps/api/v2/src/app.module.ts`
- `cal.diy/apps/api/v2/src/modules/endpoints.module.ts`
- `cal.diy/apps/api/v2/src/platform/platform-endpoints-module.ts`

REST controller inventory:

- Event types: `cal.diy/apps/api/v2/src/platform/event-types/event-types_2024_06_14/controllers/event-types.controller.ts`
- Schedules: `cal.diy/apps/api/v2/src/platform/schedules/schedules_2024_06_11/controllers/schedules.controller.ts`
- Slots: `cal.diy/apps/api/v2/src/modules/slots/slots-2024-09-04/controllers/slots.controller.ts`
- Bookings: `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/bookings.controller.ts`
- Booking attendees: `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/booking-attendees.controller.ts`
- Booking guests: `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/booking-guests.controller.ts`
- Booking location: `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/booking-location.controller.ts`
- Conferencing: `cal.diy/apps/api/v2/src/modules/conferencing/controllers/conferencing.controller.ts`
- Calendars: `cal.diy/apps/api/v2/src/platform/calendars/controllers/calendars.controller.ts`
- Selected calendars: `cal.diy/apps/api/v2/src/modules/selected-calendars/controllers/selected-calendars.controller.ts`
- Destination calendars: `cal.diy/apps/api/v2/src/modules/destination-calendars/controllers/destination-calendars.controller.ts`
- Private links: `cal.diy/apps/api/v2/src/platform/event-types-private-links/controllers/event-types-private-links.controller.ts`
- User webhooks: `cal.diy/apps/api/v2/src/modules/webhooks/controllers/webhooks.controller.ts`
- Event-type webhooks: `cal.diy/apps/api/v2/src/modules/event-types/controllers/event-types-webhooks.controller.ts`

### Web and tRPC Entry Points

The dashboard does not behave like a REST-only client. It uses Next routes, tRPC, `packages/features`, `packages/app-store`, and Prisma-facing libraries.

Public and dashboard route sources:

- Public profile page: `cal.diy/apps/web/app/(booking-page-wrapper)/[user]/page.tsx`
- Public event booking page: `cal.diy/apps/web/app/(booking-page-wrapper)/[user]/[type]/page.tsx`
- Private hashed link page: `cal.diy/apps/web/app/(booking-page-wrapper)/d/[link]/[slug]/page.tsx`
- Booking success page: `cal.diy/apps/web/app/(booking-page-wrapper)/booking-successful/[uid]/page.tsx`
- Booking detail page: `cal.diy/apps/web/app/(booking-page-wrapper)/booking/[uid]/page.tsx`
- Reschedule page: `cal.diy/apps/web/app/reschedule/[uid]/page.tsx`
- Event types dashboard: `cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/event-types/page.tsx`
- Event type editor: `cal.diy/apps/web/app/(use-page-wrapper)/event-types/[type]/page.tsx`
- Availability dashboard: `cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/availability/page.tsx`
- Schedule editor: `cal.diy/apps/web/app/(use-page-wrapper)/availability/[schedule]/page.tsx`
- Bookings dashboard: `cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/bookings/[status]/page.tsx`
- Calendar settings: `cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/calendars/page.tsx`
- Conferencing settings: `cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/conferencing/page.tsx`
- Apps dashboard: `cal.diy/apps/web/app/(use-page-wrapper)/apps/(homepage)/page.tsx`

tRPC root wiring:

- `cal.diy/packages/trpc/server/routers/viewer/_router.tsx`

Web tRPC API route inventory:

- `cal.diy/apps/web/pages/api/trpc/eventTypes/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/eventTypesHeavy/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/availability/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/slots/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/bookings/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/calendars/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/apps/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/appsRouter/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/calVideo/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/webhook/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/ooo/[trpc].ts`
- `cal.diy/apps/web/pages/api/trpc/travelSchedules/[trpc].ts`

Scheduling-relevant tRPC routers:

- Event type queries: `cal.diy/packages/trpc/server/routers/viewer/eventTypes/_router.ts`
- Event type create/update/duplicate: `cal.diy/packages/trpc/server/routers/viewer/eventTypes/heavy/_router.ts`
- Availability and schedules: `cal.diy/packages/trpc/server/routers/viewer/availability/_router.tsx`
- Schedule nested router: `cal.diy/packages/trpc/server/routers/viewer/availability/schedule/_router.tsx`
- Slots: `cal.diy/packages/trpc/server/routers/viewer/slots/_router.tsx`
- Bookings: `cal.diy/packages/trpc/server/routers/viewer/bookings/_router.tsx`
- Apps and default conferencing: `cal.diy/packages/trpc/server/routers/viewer/apps/_router.tsx`
- Calendars: `cal.diy/packages/trpc/server/routers/viewer/calendars/_router.tsx`
- Cal Video: `cal.diy/packages/trpc/server/routers/viewer/calVideo/_router.tsx`
- Webhooks: `cal.diy/packages/trpc/server/routers/viewer/webhook/_router.tsx`

### Package-Level Business-Rule Seams

Primary platform library exports:

- Event types: `cal.diy/packages/platform/libraries/event-types.ts`
- Schedules: `cal.diy/packages/platform/libraries/schedules.ts`
- Slots: `cal.diy/packages/platform/libraries/slots.ts`
- Bookings: `cal.diy/packages/platform/libraries/bookings.ts`
- Calendars: `cal.diy/packages/platform/libraries/calendars.ts`
- Conferencing: `cal.diy/packages/platform/libraries/conferencing.ts`
- Private links: `cal.diy/packages/platform/libraries/private-links.ts`

Business-rule implementation seams:

- Event type service/repository: `cal.diy/packages/features/eventtypes/service/EventTypeService.ts`, `cal.diy/packages/features/eventtypes/repositories/eventTypeRepository.ts`
- Public event lookup: `cal.diy/packages/features/eventtypes/lib/getPublicEvent.ts`
- Event type lookup/listing: `cal.diy/packages/features/eventtypes/lib/getEventTypeById.ts`, `cal.diy/packages/features/eventtypes/lib/getEventTypesByViewer.ts`, `cal.diy/packages/features/eventtypes/lib/getEventTypesPublic.ts`
- Event naming: `cal.diy/packages/features/eventtypes/lib/eventNaming.ts`
- Booking fields: `cal.diy/packages/features/eventtypes/lib/bookingFieldsManager.ts`
- Schedules: `cal.diy/packages/features/schedules/services/ScheduleService.ts`, `cal.diy/packages/features/schedules/repositories/ScheduleRepository.ts`
- Slot generation: `cal.diy/packages/features/schedules/lib/slots.ts`
- User availability: `cal.diy/packages/features/availability/lib/getUserAvailability.ts`
- Aggregated availability: `cal.diy/packages/features/availability/lib/getAggregatedAvailability/getAggregatedAvailability.ts`
- Busy times: `cal.diy/packages/features/busyTimes/services/getBusyTimes.ts`
- Booking limit busy-time conversion: `cal.diy/packages/features/busyTimes/lib/getBusyTimesFromLimits.ts`
- Booking create: `cal.diy/packages/features/bookings/lib/handleNewBooking/createBooking.ts`
- Booking validation and event loading: `cal.diy/packages/features/bookings/lib/handleNewBooking/getBookingData.ts`, `cal.diy/packages/features/bookings/lib/handleNewBooking/getEventType.ts`, `cal.diy/packages/features/bookings/lib/handleNewBooking/loadAndValidateUsers.ts`
- Booking limits: `cal.diy/packages/features/bookings/lib/checkBookingLimits.ts`, `cal.diy/packages/features/bookings/lib/checkDurationLimits.ts`, `cal.diy/packages/features/bookings/lib/handleNewBooking/checkBookingAndDurationLimits.ts`
- Booking availability check: `cal.diy/packages/features/bookings/lib/handleNewBooking/ensureAvailableUsers.ts`
- Booking service orchestration: `cal.diy/packages/features/bookings/lib/service/RegularBookingService.ts`, `cal.diy/packages/features/bookings/lib/service/RecurringBookingService.ts`
- Booking cancellation: `cal.diy/packages/features/bookings/lib/handleCancelBooking.ts`
- Booking confirmation: `cal.diy/packages/features/bookings/lib/handleConfirmation.ts`
- Guests and attendees: `cal.diy/packages/features/bookings/lib/getHostsAndGuests.ts`, `cal.diy/packages/features/bookings/lib/getCalEventResponses.ts`, `cal.diy/packages/features/bookings/services/BookingAttendeesService.ts`, `cal.diy/packages/features/bookings/services/BookingAttendeesRemoveService.ts`
- No-show: `cal.diy/packages/features/handleMarkNoShow.ts`, `cal.diy/packages/features/noShow/handleSendingAttendeeNoShowDataToApps.ts`
- Webhooks: `cal.diy/packages/features/webhooks/lib/WebhookService.ts`, `cal.diy/packages/features/webhooks/lib/service/BookingWebhookService.ts`, `cal.diy/packages/features/webhooks/lib/sendOrSchedulePayload.ts`
- Daily.co/Cal Video: `cal.diy/packages/features/conferencing/lib/videoClient.ts`, `cal.diy/packages/app-store/dailyvideo/lib/VideoApiAdapter.ts`
- Selected calendars: `cal.diy/packages/features/selectedCalendar/repositories/SelectedCalendarRepository.ts`
- Calendar user shape: `cal.diy/packages/lib/server/withSelectedCalendars.ts`
- Calendar event builder: `cal.diy/packages/features/CalendarEventBuilder.test.ts` and calendar-event builders imported by booking code
- Private links: `cal.diy/packages/features/hashedLink/lib/service/HashedLinkService.ts`, `cal.diy/packages/features/hashedLink/lib/repository/HashedLinkRepository.ts`

### Database Entities

Primary Prisma source:

- `cal.diy/packages/prisma/schema.prisma`

Scheduling-relevant models:

- `EventType`
- `Schedule`
- `Availability`
- `SelectedCalendar`
- `DestinationCalendar`
- `Booking`
- `BookingReference`
- `Attendee`
- `BookingSeat`
- `HashedLink`
- `Webhook`
- `WebhookScheduledTriggers`
- `Credential`
- `App`
- `OutOfOfficeEntry`
- `EventTypeCustomInput`
- `EventTypeTranslation`
- `BookingReport`
- `BookingInternalNote`
- `BookingAudit`

## Flow: Service/Event Type Creation and Management

### Docs Intent

The docs matrix marks event types, recurring event types, seated events, paid events, private links, booking management, buffer times, minimum notice, booking limits, and API v2 as supported in Cal.diy. It marks team event types, round-robin, collective, managed event types, and organizations as not supported in Cal.diy.

### REST API

Controller: `cal.diy/apps/api/v2/src/platform/event-types/event-types_2024_06_14/controllers/event-types.controller.ts`

Routes:

- `POST /v2/event-types`
- `GET /v2/event-types`
- `GET /v2/event-types/:eventTypeId`
- `PATCH /v2/event-types/:eventTypeId`
- `DELETE /v2/event-types/:eventTypeId`

Create input source:

- `cal.diy/packages/platform/types/event-types/event-types_2024_06_14/inputs/create-event-type.input.ts`

Create request shape:

- Required: `lengthInMinutes`, `title`, `slug`
- Optional core: `description`, `locations`, `scheduleId`, `slotInterval`, `minimumBookingNotice`, `beforeEventBuffer`, `afterEventBuffer`, `hidden`, `bookingRequiresAuthentication`
- Optional form behavior: `bookingFields`, `disableGuests`, `customName`, `interfaceLanguage`
- Optional limits: `bookingLimitsCount`, `bookingLimitsDuration`, `bookerActiveBookingsLimit`, `bookingWindow`, `onlyShowFirstAvailableSlot`
- Optional booking page behavior: `offsetStart`, `bookerLayouts`, `successRedirectUrl`, `lockTimeZoneToggleOnBookingPage`, `hideOrganizerEmail`
- Optional booking policy: `confirmationPolicy`, `disableCancelling`, `disableRescheduling`, `allowReschedulingPastBookings`, `allowReschedulingCancelledBookings`
- Optional advanced: `recurrence`, `seats`, `destinationCalendar`, `useDestinationCalendarEmail`, `hideCalendarNotes`, `hideCalendarEventDetails`, `requiresBookerEmailVerification`, `calVideoSettings`, `showOptimizedSlots`

Response shape:

- Cal API wrapper: `{ status, data }`
- `data` is the transformed event type output from the API output pipe.

The controller transforms API input through `InputEventTypesService`, then calls the platform event-type service, which calls `createEventType` or `updateEventType` from platform libraries.

### Web/tRPC

tRPC route sources:

- `cal.diy/packages/trpc/server/routers/viewer/eventTypes/_router.ts`
- `cal.diy/packages/trpc/server/routers/viewer/eventTypes/heavy/_router.ts`

Dashboard procedures:

- `viewer.eventTypes.getByViewer`
- `viewer.eventTypes.getUserEventGroups`
- `viewer.eventTypes.getEventTypesFromGroup`
- `viewer.eventTypes.getActiveOnOptions`
- `viewer.eventTypes.list`
- `viewer.eventTypes.listWithTeam`
- `viewer.eventTypes.get`
- `viewer.eventTypes.delete`
- `viewer.eventTypes.bulkEventFetch`
- `viewer.eventTypes.bulkUpdateToDefaultLocation`
- `viewer.eventTypes.getHashedLink`
- `viewer.eventTypes.getHashedLinks`
- `viewer.eventTypesHeavy.create`
- `viewer.eventTypesHeavy.duplicate`
- `viewer.eventTypesHeavy.update`

Dashboard route sources:

- `cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/event-types/page.tsx`
- `cal.diy/apps/web/app/(use-page-wrapper)/event-types/[type]/page.tsx`
- `cal.diy/apps/web/modules/event-types`

The dashboard uses tRPC and server callers, not REST v2, for event-type grouping, editor data, duplication, deletion, default-location bulk changes, and hashed-link lookups.

### Business-Rule Seams

- `cal.diy/packages/platform/libraries/event-types.ts`
- `cal.diy/packages/features/eventtypes/service/EventTypeService.ts`
- `cal.diy/packages/features/eventtypes/repositories/eventTypeRepository.ts`
- `cal.diy/packages/features/eventtypes/lib/getPublicEvent.ts`
- `cal.diy/packages/features/eventtypes/lib/getEventTypeById.ts`
- `cal.diy/packages/features/eventtypes/lib/eventNaming.ts`
- `cal.diy/packages/features/eventtypes/lib/bookingFieldsManager.ts`
- `cal.diy/packages/trpc/server/routers/viewer/eventTypes/heavy/create.handler.ts`
- `cal.diy/packages/trpc/server/routers/viewer/eventTypes/heavy/update.handler.ts`
- `cal.diy/apps/api/v2/src/platform/event-types/event-types_2024_06_14/services/input-event-types.service.ts`

### Database Entities

- `EventType`
- `EventTypeCustomInput`
- `EventTypeTranslation`
- `Schedule`
- `Availability`
- `SelectedCalendar`
- `DestinationCalendar`
- `Webhook`
- `HashedLink`
- `Credential`
- `App`

### Nerova Port Decision

Copy the solo service/event-type core:

- title
- slug
- duration
- variable duration options
- description
- locations
- availability schedule binding
- buffers
- minimum notice
- booking window
- booking count and duration limits
- custom booking fields
- private visibility
- disable rescheduling/cancelling policy
- destination calendar selection
- Cal Video location as the default video location

Simplify for first MVP:

- one tenant-owned service catalog
- solo providers only
- no team, round-robin, collective, managed, or organization event types
- no paid events through Cal.diy semantics until Nerova payment policy is designed
- no recurrence/seated events in the first slice unless a real customer requirement appears
- no full Cal app-store toggle model in the first event-type slice

Defer:

- recurring event types
- seated events
- paid event types
- per-host locations
- managed event inheritance
- round-robin host assignment
- route-form assignment
- event-type translations

Reject for the first port:

- using Cal.diy organization/team semantics as Nerova tenant semantics. PlatformPlatform already owns tenancy and identity.

### API v2 Gaps

REST v2 can create and update event types, but the dashboard contract is larger than REST:

- Dashboard grouping and list behavior come from `viewer.eventTypes.getUserEventGroups`, `getEventTypesFromGroup`, and `EventGroupBuilder`.
- Event editor mutations use `viewer.eventTypesHeavy.create/update/duplicate`.
- Host/location assignment helpers are tRPC-only.
- Some app-store and location options are resolved through `viewer.apps.locationOptions`, not the event-type REST controller.
- Public booking behavior depends on `getPublicEvent` and web route loaders, not just `GET /v2/event-types/:eventTypeId`.

Nerova must treat REST DTOs as useful external API shapes, but tRPC and package sources are the behavior oracle.

## Flow: Schedule and Availability Setup

### Docs Intent

The docs matrix marks availability schedules, date overrides, buffer times, minimum notice, booking limits, travel schedules, and out-of-office as supported. Schedules are the availability source used by event types unless an event type is tied to a specific schedule.

### REST API

Controller: `cal.diy/apps/api/v2/src/platform/schedules/schedules_2024_06_11/controllers/schedules.controller.ts`

Routes:

- `POST /v2/schedules`
- `GET /v2/schedules/default`
- `GET /v2/schedules/:scheduleId`
- `GET /v2/schedules`
- `PATCH /v2/schedules/:scheduleId`
- `DELETE /v2/schedules/:scheduleId`

Input source:

- `cal.diy/packages/platform/types/schedules/schedules-2024-06-11/inputs/create-schedule.input.ts`

Create request shape:

- Required: `name`, `timeZone`, `isDefault`
- Optional `availability`: array of `{ days, startTime, endTime }`
- Optional `overrides`: array of `{ date, startTime, endTime }`

Time shape:

- Weekly availability uses weekday names and `HH:MM`.
- Overrides use ISO dates and `HH:MM`.
- Time zone is required because schedule windows are interpreted in the schedule owner's local time.

Response shape:

- Cal API wrapper: `{ status, data }`
- `data` contains schedule details, availability windows, overrides, `timeZone`, and default-schedule state.

### Web/tRPC

tRPC route sources:

- `cal.diy/packages/trpc/server/routers/viewer/availability/_router.tsx`
- `cal.diy/packages/trpc/server/routers/viewer/availability/schedule/_router.tsx`

Dashboard procedures:

- `viewer.availability.list`
- `viewer.availability.user`
- `viewer.availability.listTeam`
- `viewer.availability.calendarOverlay`
- `viewer.availability.schedule.get`
- `viewer.availability.schedule.create`
- `viewer.availability.schedule.update`
- `viewer.availability.schedule.delete`
- `viewer.availability.schedule.duplicate`
- `viewer.availability.schedule.getScheduleByUserId`
- `viewer.availability.schedule.getAllSchedulesByUserId`
- `viewer.availability.schedule.getScheduleByEventSlug`
- `viewer.availability.schedule.bulkUpdateToDefaultAvailability`

Dashboard route sources:

- `cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/availability/page.tsx`
- `cal.diy/apps/web/app/(use-page-wrapper)/availability/[schedule]/page.tsx`
- `cal.diy/apps/web/modules/availability`
- `cal.diy/apps/web/modules/schedules`

### Business-Rule Seams

- `cal.diy/packages/platform/libraries/schedules.ts`
- `cal.diy/packages/features/schedules/services/ScheduleService.ts`
- `cal.diy/packages/features/schedules/repositories/ScheduleRepository.ts`
- `cal.diy/packages/features/availability/lib/getUserAvailability.ts`
- `cal.diy/packages/features/availability/lib/detectEventTypeScheduleForUser.ts`
- `cal.diy/packages/features/availability/lib/getAggregatedAvailability/getAggregatedAvailability.ts`
- `cal.diy/packages/features/schedules/lib/date-ranges.ts`

`getAggregatedAvailability` intersects fixed-host availability and handles round-robin groups. That logic exists in source but is outside the Cal.diy documented solo surface.

### Database Entities

- `Schedule`
- `Availability`
- `EventType`
- `OutOfOfficeEntry`
- `SelectedCalendar`

### Nerova Port Decision

Copy:

- availability schedule aggregate
- default schedule per provider
- named schedules
- time-zone-aware weekly windows
- date overrides
- event type to schedule binding

Simplify:

- schedules belong to a Nerova tenant provider, not a Cal user profile
- use Nerova tenancy and authorization
- store availability in EF Core-owned aggregates, not Prisma shape
- start with solo-provider availability only

Defer:

- travel schedules
- out-of-office delegation behavior
- team availability intersections
- holiday conflict UX
- schedule bulk update actions

### API v2 Gaps

REST v2 exposes schedule CRUD, but the dashboard depends on tRPC for duplicate, calendar overlay, schedule-by-event-slug, team availability, and bulk default availability updates.

For the first Nerova slice, REST schedule DTOs are enough for basic create/update/list behavior. The acceptance contract still needs package rules for date range normalization and time-zone handling.

## Flow: Slot Calculation and Reservation

### Docs Intent

The docs matrix marks booking management, availability schedules, buffers, minimum notice, booking limits, selected calendars through calendar integrations, and API v2 as supported. Slot calculation is the convergence point for all of those rules.

### REST API

Controller: `cal.diy/apps/api/v2/src/modules/slots/slots-2024-09-04/controllers/slots.controller.ts`

Routes:

- `GET /v2/slots`
- `POST /v2/slots/reservations`
- `GET /v2/slots/reservations/:uid`
- `PATCH /v2/slots/reservations/:uid`
- `DELETE /v2/slots/reservations/:uid`

Query input source:

- `cal.diy/packages/platform/types/slots/slots-2024-09-04/inputs/get-slots.input.ts`
- `cal.diy/packages/platform/types/slots/slots-2024-09-04/inputs/get-slots-input.pipe.ts`

Slot query shapes:

- By event type id: `type=byEventTypeId`, `eventTypeId`, `start`, `end`
- By user and event slug: `type=byUsernameAndEventTypeSlug`, `username`, `eventTypeSlug`, optional `organizationSlug`, `start`, `end`
- By team and event slug: `type=byTeamSlugAndEventTypeSlug`, `teamSlug`, `eventTypeSlug`, optional `organizationSlug`, `start`, `end`
- By usernames for dynamic availability: `type=byUsernames`, `usernames`, `organizationSlug`, `start`, `end`
- Optional: `timeZone`, `duration`, `format`, `bookingUidToReschedule`

Reservation input source:

- `cal.diy/packages/platform/types/slots/slots-2024-09-04/inputs/reserve-slot.input.ts`

Reservation request shape:

- Required: `eventTypeId`, `slotStart`
- Optional: `slotDuration`, `reservationDuration`

Response shape:

- Slot response is a Cal API wrapper with date-keyed slot groups. `format=range` returns start/end ranges; `format=time` returns times. Seated slots can include attendee count and booking uid.
- Reservation response is a Cal API wrapper containing the selected slot reservation, including reservation uid and expiry semantics.

### Web/tRPC

tRPC route source:

- `cal.diy/packages/trpc/server/routers/viewer/slots/_router.tsx`

Procedures:

- `viewer.slots.getSchedule`
- `viewer.slots.reserveSlot`
- `viewer.slots.isAvailable`
- `viewer.slots.removeSelectedSlotMark`

The router itself notes that `getSchedule` should be called `getAvailableSlots`. Public booking pages use this public tRPC route and temporary selected-slot cookies/records.

### Business-Rule Seams

- `cal.diy/packages/platform/libraries/slots.ts`
- `cal.diy/packages/features/schedules/lib/slots.ts`
- `cal.diy/packages/features/availability/lib/getUserAvailability.ts`
- `cal.diy/packages/features/availability/lib/getAggregatedAvailability/getAggregatedAvailability.ts`
- `cal.diy/packages/features/busyTimes/services/getBusyTimes.ts`
- `cal.diy/packages/features/busyTimes/lib/getBusyTimesFromLimits.ts`
- `cal.diy/packages/features/slots/handleNotificationWhenNoSlots.ts`
- `cal.diy/packages/features/selectedSlots/repositories/PrismaSelectedSlotRepository.ts`
- `cal.diy/apps/api/v2/src/modules/slots/slots-2024-09-04/services/slots.service.ts`
- `cal.diy/apps/api/v2/src/modules/slots/slots-2024-09-04/services/slots-output.service.ts`

Important behavior in `cal.diy/packages/features/schedules/lib/slots.ts`:

- minimum interval and event length are clamped to at least one minute
- date ranges are sorted
- duplicate slot start times are collapsed
- slot interval is selected from 60, 30, 20, 15, 10, 5, or environment fallback
- minimum booking notice is applied relative to current UTC time
- seconds and milliseconds are normalized for same-day bookings
- slot rounding is done in the invitee time zone
- `offsetStart` shifts slot starts
- `showOptimizedSlots` changes rounding to preserve maximum slots
- out-of-office dates can mark slots as away
- a slot is only emitted when the full event length fits before the range end

### Database Entities

- `EventType`
- `Schedule`
- `Availability`
- `SelectedCalendar`
- `DestinationCalendar`
- `Booking`
- `BookingSeat`
- `OutOfOfficeEntry`
- Selected slot reservation storage from the selected-slots feature

### Nerova Port Decision

Copy after Service/Event Types + Availability:

- slot generation algorithm
- minimum notice behavior
- buffer-aware range exclusion
- booking-window filtering
- selected-calendar busy-time exclusion
- reschedule slot inclusion via existing booking uid
- temporary slot reservations

Simplify:

- no dynamic multi-user slots for the first MVP
- no team or round-robin slots
- no seated slot attendee counts until seated events are a planned feature
- no no-slots notification automation in first implementation

Defer:

- optimized slots
- out-of-office slot annotations
- travel schedules
- route-form host subset behavior

### API v2 Gaps

The REST endpoint exposes slots, but the acceptance contract cannot be derived from the endpoint alone. Real slot behavior lives in `getUserAvailability`, busy-time services, booking-limit conversion, selected-slot reservation, and `schedules/lib/slots.ts`.

Nerova must port the package-level rule chain, not just expose a `GET /slots` equivalent.

## Flow: Booking Create, Reschedule, and Cancel

### Docs Intent

The docs matrix marks booking management, recurring events, seated events, paid events, webhooks, calendar integrations, and conferencing as supported. Booking is the largest behavior surface because it writes the booking and triggers side effects.

### REST API

Controller: `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/bookings.controller.ts`

Routes:

- `POST /v2/bookings`
- `GET /v2/bookings`
- `GET /v2/bookings/:bookingUid`
- `GET /v2/bookings/by-seat/:seatUid`
- `POST /v2/bookings/:bookingUid/reschedule`
- `POST /v2/bookings/:bookingUid/cancel`
- `GET /v2/bookings/:bookingUid/calendar-links`
- `GET /v2/bookings/:bookingUid/references`
- `GET /v2/bookings/:bookingUid/conferencing-sessions`
- `GET /v2/bookings/:bookingUid/recordings`
- `GET /v2/bookings/:bookingUid/transcripts`

Create input source:

- `cal.diy/packages/platform/types/bookings/2024-08-13/inputs/create-booking.input.ts`

Create request shape:

- Required: `start`, `attendee`
- Event identification: `eventTypeId`, or `eventTypeSlug + username`, or `eventTypeSlug + teamSlug`, with optional `organizationSlug`
- Attendee: `name`, `timeZone`, optional `email`, optional `phoneNumber`, optional `language`
- Optional: `bookingFieldsResponses`, `guests`, `meetingUrl` deprecated, `location`, `metadata`, `lengthInMinutes`, `routing`, `emailVerificationCode`
- Recurring create extends create with optional `recurrenceCount`

Reschedule input source:

- `cal.diy/packages/platform/types/bookings/2024-08-13/inputs/reschedule-booking.input.ts`

Reschedule request shape:

- Required: `start`
- Optional: `rescheduledBy`, `reschedulingReason`, `emailVerificationCode`
- Seated reschedule includes required `seatUid`

Cancel input source:

- `cal.diy/packages/platform/types/bookings/2024-08-13/inputs/cancel-booking.input.ts`

Cancel request shape:

- Optional: `cancellationReason`, `cancelSubsequentBookings`
- Seated cancel includes required `seatUid`

Response shape:

- Cal API wrapper: `{ status, data }`
- Create and reschedule return booking output. Cancel returns cancelled booking output.

### Web/tRPC

tRPC route source:

- `cal.diy/packages/trpc/server/routers/viewer/bookings/_router.tsx`

Dashboard procedures:

- `viewer.bookings.get`
- `viewer.bookings.find`
- `viewer.bookings.requestReschedule`
- `viewer.bookings.editLocation`
- `viewer.bookings.addGuests`
- `viewer.bookings.confirm`
- `viewer.bookings.getBookingAttendees`
- `viewer.bookings.getBookingDetails`
- `viewer.bookings.getBookingHistory`
- `viewer.bookings.reportBooking`
- `viewer.bookings.reportWrongAssignment`

Web route sources:

- `cal.diy/apps/web/app/(use-page-wrapper)/(main-nav)/bookings/[status]/page.tsx`
- `cal.diy/apps/web/app/(booking-page-wrapper)/booking/[uid]/page.tsx`
- `cal.diy/apps/web/app/reschedule/[uid]/page.tsx`
- `cal.diy/apps/web/app/(booking-page-wrapper)/booking-successful/[uid]/page.tsx`

### Business-Rule Seams

- `cal.diy/packages/platform/libraries/bookings.ts`
- `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/services/bookings.service.ts`
- `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/services/input.service.ts`
- `cal.diy/packages/features/bookings/lib/service/RegularBookingService.ts`
- `cal.diy/packages/features/bookings/lib/service/RecurringBookingService.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/getBookingData.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/getEventType.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/getEventTypesFromDB.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/loadAndValidateUsers.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/validateBookingTimeIsNotOutOfBounds.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/validateEventLength.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/checkBookingAndDurationLimits.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/ensureAvailableUsers.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/createBooking.ts`
- `cal.diy/packages/features/bookings/lib/handleCancelBooking.ts`
- `cal.diy/packages/features/bookings/lib/onBookingEvents/BookingEventHandlerService.ts`
- `cal.diy/packages/features/bookings/lib/BookingEmailSmsHandler.ts`
- `cal.diy/packages/features/webhooks/lib/service/BookingWebhookService.ts`
- `cal.diy/packages/features/conferencing/lib/videoClient.ts`

### Database Entities

- `Booking`
- `Attendee`
- `BookingReference`
- `BookingSeat`
- `EventType`
- `Schedule`
- `SelectedCalendar`
- `DestinationCalendar`
- `Credential`
- `Webhook`
- `BookingAudit`
- Payment entities where paid events are enabled

### Nerova Port Decision

Defer until the service/event-type, availability, calendar, and slot contracts exist.

Copy later:

- normal solo booking create
- booking field responses
- calendar event reference creation
- destination-calendar selection
- selected-calendar conflict checks
- reschedule by replacing/cancelling the original booking as Cal.diy does
- cancellation reason
- calendar links
- webhook event emission
- email/SMS notification hooks adapted to Nerova channels

Simplify for first booking MVP:

- one provider, one service, one attendee
- no recurrence
- no seated bookings
- no paid event behavior
- no dynamic team booking
- no route-form assignment
- no booking reports or wrong-assignment reports

Defer:

- recurring bookings
- seated bookings
- no-show billing
- paid bookings
- booking audit replay UI
- conferencing recordings/transcripts

### API v2 Gaps

REST v2 gives create/reschedule/cancel endpoints, but booking correctness depends on package-level side effects:

- conflict checks and selected calendar reads
- booking limits and duration limits
- calendar event creation/update/delete
- video room creation
- webhook delivery
- attendee and organizer notifications
- reschedule and cancellation semantics
- payment/refund/no-show behavior

Nerova cannot safely port bookings by copying REST DTOs only.

## Flow: Booking Confirmation, No-Show, Attendees, and Guests

### Docs Intent

The docs matrix marks booking management, webhooks, messaging integrations, and paid events as supported. Confirmation and no-show behavior are operational booking lifecycle states rather than simple CRUD.

### REST API

Booking confirmation routes:

- `POST /v2/bookings/:bookingUid/confirm`
- `POST /v2/bookings/:bookingUid/decline`

No-show route:

- `POST /v2/bookings/:bookingUid/mark-absent`

No-show input source:

- `cal.diy/packages/platform/types/bookings/2024-08-13/inputs/mark-absent.input.ts`

No-show request shape:

- Optional `host`
- Optional `attendees`: array of `{ email, absent }`

Attendee controller:

- `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/booking-attendees.controller.ts`

Attendee routes:

- `GET /v2/bookings/:bookingUid/attendees`
- `GET /v2/bookings/:bookingUid/attendees/:attendeeId`
- `POST /v2/bookings/:bookingUid/attendees`
- `DELETE /v2/bookings/:bookingUid/attendees/:attendeeId`

Add attendee input source:

- `cal.diy/packages/platform/types/bookings/2024-08-13/inputs/add-attendee.input.ts`

Add attendee request shape:

- `name`, `timeZone`, `email`
- Optional `phoneNumber`, `language`

Guest controller:

- `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/booking-guests.controller.ts`

Guest route:

- `POST /v2/bookings/:bookingUid/guests`

Add guests input source:

- `cal.diy/packages/platform/types/bookings/2024-08-13/inputs/add-guests.input.ts`

Add guests request shape:

- `guests`: 1 to 10 guests per request
- Each guest has `email`, optional `name`, optional `timeZone`, optional `phoneNumber`, optional `language`

Controller behavior notes from source:

- Guests are capped at 10 per request and 30 total.
- Attendee add/remove is rate-limited.
- Primary attendee cannot be removed through attendee deletion.
- Attendee/guest mutations update calendar events and send notifications.

### Web/tRPC

tRPC route source:

- `cal.diy/packages/trpc/server/routers/viewer/bookings/_router.tsx`

Procedures:

- `viewer.bookings.confirm`
- `viewer.bookings.addGuests`
- `viewer.bookings.getBookingAttendees`
- `viewer.bookings.editLocation`
- `viewer.bookings.getBookingHistory`

### Business-Rule Seams

- `cal.diy/packages/features/bookings/lib/handleConfirmation.ts`
- `cal.diy/packages/features/handleMarkNoShow.ts`
- `cal.diy/packages/features/noShow/handleSendingAttendeeNoShowDataToApps.ts`
- `cal.diy/packages/features/bookings/services/BookingAttendeesService.ts`
- `cal.diy/packages/features/bookings/services/BookingAttendeesRemoveService.ts`
- `cal.diy/packages/features/bookings/lib/getHostsAndGuests.ts`
- `cal.diy/packages/features/bookings/lib/getCalEventResponses.ts`
- `cal.diy/packages/features/bookings/lib/onBookingEvents/BookingEventHandlerService.ts`
- `cal.diy/packages/features/bookings/lib/tasker/BookingEmailAndSmsTaskService.ts`
- `cal.diy/packages/features/webhooks/lib/service/BookingWebhookService.ts`

### Database Entities

- `Booking`
- `Attendee`
- `BookingReference`
- `BookingSeat`
- `BookingAudit`
- `Webhook`
- Payment/no-show-related entities where paid events are enabled

### Nerova Port Decision

Defer until normal booking create/reschedule/cancel is stable.

Copy later:

- confirmation status
- decline reason
- host and attendee absence tracking
- guest additions
- calendar event attendee updates
- lifecycle webhooks

Simplify:

- first booking MVP can skip guest management and no-show automation
- support a simple confirmed/cancelled/rescheduled lifecycle before advanced confirmation policy
- use Nerova notification pipeline rather than Cal.diy mail/SMS tasker shape

Reject for first MVP:

- no-show payment penalties until payment policy is explicitly designed
- wrong-assignment reports because solo-provider MVP does not need them

### API v2 Gaps

REST exposes the lifecycle endpoints, but much of the behavior is in service packages and taskers. Notifications, app no-show callbacks, calendar event mutation, and webhook emission must be ported from packages.

## Flow: Private Links and Public Booking Pages

### Docs Intent

The docs matrix marks private links, booking management, embed, and API v2 as supported. Public booking pages are the main customer-facing scheduling surface in Cal.diy.

### REST API

Private link controller:

- `cal.diy/apps/api/v2/src/platform/event-types-private-links/controllers/event-types-private-links.controller.ts`

Routes:

- `POST /v2/event-types/:eventTypeId/private-links`
- `GET /v2/event-types/:eventTypeId/private-links`
- `PATCH /v2/event-types/:eventTypeId/private-links/:linkId`
- `DELETE /v2/event-types/:eventTypeId/private-links/:linkId`

Response shape:

- Cal API wrapper with private link data.

Public booking pages are not a REST-only feature. They are Next routes backed by server loaders and package functions.

### Web/tRPC

Web route sources:

- `cal.diy/apps/web/app/(booking-page-wrapper)/[user]/page.tsx`
- `cal.diy/apps/web/app/(booking-page-wrapper)/[user]/[type]/page.tsx`
- `cal.diy/apps/web/app/(booking-page-wrapper)/[user]/[type]/embed/page.tsx`
- `cal.diy/apps/web/app/(booking-page-wrapper)/d/[link]/[slug]/page.tsx`
- `cal.diy/apps/web/pagesAndRewritePaths.ts`

tRPC sources:

- `viewer.eventTypes.getHashedLink`
- `viewer.eventTypes.getHashedLinks`
- `viewer.slots.getSchedule`
- `viewer.slots.reserveSlot`
- `viewer.slots.isAvailable`

### Business-Rule Seams

- `cal.diy/packages/platform/libraries/private-links.ts`
- `cal.diy/packages/features/hashedLink/lib/service/HashedLinkService.ts`
- `cal.diy/packages/features/hashedLink/lib/repository/HashedLinkRepository.ts`
- `cal.diy/packages/features/eventtypes/lib/getPublicEvent.ts`
- `cal.diy/packages/features/eventtypes/lib/getEventTypesPublic.ts`
- `cal.diy/apps/web/server/lib/[user]/[type]/getServerSideProps`
- `cal.diy/apps/web/modules/bookings`
- `cal.diy/packages/features/bookings/Booker`

### Database Entities

- `HashedLink`
- `EventType`
- `Schedule`
- `Availability`
- `SelectedCalendar`
- `Booking`

### Nerova Port Decision

Copy:

- public service URL model
- private booking links
- private link expiry/validity behavior
- public booking page reads from service/event type and slots

Simplify:

- use Nerova tenant/provider/service URLs, not Cal username/org slug routing
- first MVP can expose Nerova-branded booking pages inside our platform
- no full embed surface until the booking page is stable

Defer:

- embed popup/floating-button modes
- dynamic group booking pages
- organization route rewrites
- Cal.diy profile-page aggregation if Nerova chooses a different service catalog UX

### API v2 Gaps

Private link CRUD is in REST, but public booking pages are not. Public page behavior depends on Next route loaders, route rewrite rules, `getPublicEvent`, Booker components, public tRPC slots, and selected-slot reservations.

Nerova should port the public page behavior as a first-party Nerova web flow, not attempt to embed Cal.diy pages.

## Flow: Daily.co and Video Location Behavior

### Docs Intent

The docs matrix marks Cal Video powered by Daily.co, Zoom, Google Meet, Microsoft Teams, Webex, Jitsi, and other conferencing apps as supported. It marks Cal.com Video Recordings as not supported in Cal.diy, even though source contains recording/transcript access routes and Daily.co adapter logic.

### REST API

Conferencing controller:

- `cal.diy/apps/api/v2/src/modules/conferencing/controllers/conferencing.controller.ts`

Routes:

- `POST /v2/conferencing/:app/connect`
- `GET /v2/conferencing/:app/oauth/auth-url`
- `GET /v2/conferencing/:app/oauth/callback`
- `GET /v2/conferencing`
- `POST /v2/conferencing/:app/default`
- `GET /v2/conferencing/default`
- `DELETE /v2/conferencing/:app/disconnect`

Booking location controller:

- `cal.diy/apps/api/v2/src/platform/bookings/2024-08-13/controllers/booking-location.controller.ts`

Route:

- `PATCH /v2/bookings/:bookingUid/location`

Event type location input:

- Event type location can be address, link, phone, attendee address, attendee phone, attendee defined, or integration location.
- The source comments state Cal Video is installed by default. Google Meet, Microsoft Teams, and Zoom can be installed through API. Other conferencing apps must be connected via the Cal.diy web app.

Booking location behavior from source:

- Google Meet requires Google Calendar to be connected. Without a Google calendar event, source falls back to Cal Video.
- Microsoft Teams uses Office365 calendar if connected, otherwise direct Microsoft Teams video integration.

### Web/tRPC

tRPC route sources:

- `cal.diy/packages/trpc/server/routers/viewer/apps/_router.tsx`
- `cal.diy/packages/trpc/server/routers/viewer/calVideo/_router.tsx`
- `cal.diy/packages/trpc/server/routers/viewer/bookings/_router.tsx`

Procedures:

- `viewer.apps.integrations`
- `viewer.apps.locationOptions`
- `viewer.apps.getUsersDefaultConferencingApp`
- `viewer.apps.setDefaultConferencingApp`
- `viewer.apps.updateUserDefaultConferencingApp`
- `viewer.apps.appCredentialsByType`
- `viewer.bookings.editLocation`
- `viewer.calVideo.getMeetingInformation`
- `viewer.calVideo.getCalVideoRecordings`
- `viewer.calVideo.getDownloadLinkOfCalVideoRecordings`

Web route sources:

- `cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/conferencing/page.tsx`
- `cal.diy/apps/web/app/(use-page-wrapper)/video/[uid]/page.tsx`
- `cal.diy/apps/web/app/(use-page-wrapper)/video/meeting-ended/[uid]/page.tsx`
- `cal.diy/apps/web/app/(use-page-wrapper)/video/meeting-not-started/[uid]/page.tsx`

### Business-Rule Seams

- `cal.diy/packages/platform/libraries/conferencing.ts`
- `cal.diy/packages/features/conferencing/lib/videoClient.ts`
- `cal.diy/packages/app-store/dailyvideo/lib/VideoApiAdapter.ts`
- `cal.diy/packages/app-store/dailyvideo/lib/getDailyAppKeys.ts`
- `cal.diy/packages/app-store/dailyvideo/lib/dailyApiFetcher.ts`
- `cal.diy/apps/api/v2/src/modules/conferencing/services/conferencing.service.ts`
- `cal.diy/packages/features/bookings/lib/handleNewBooking/getVideoCallDetails.ts`

### Database Entities

- `Credential`
- `App`
- `BookingReference`
- `Booking`
- `EventType`

### Nerova Port Decision

Copy:

- Cal Video as the default video location
- Daily.co room creation through a Nerova adapter
- booking reference storage for video rooms
- default conferencing app setting where relevant

Simplify:

- first MVP can support only Cal Video/Daily.co and manual link locations
- avoid full Cal.diy app-store credential UI in the first scheduling slice
- keep video rooms under Nerova-owned integration settings

Defer:

- Zoom, Google Meet, Microsoft Teams, Webex, Jitsi
- recordings and transcripts
- org/team callback routing
- per-user default conferencing apps if providers are tenant-managed instead

### API v2 Gaps

The API can connect and set default conferencing apps, but app-store management and location option behavior are dashboard/tRPC/package-driven. Event type location validity also depends on installed credentials and app-store metadata.

Nerova should implement a small integration abstraction first, with Daily.co as a first-class provider.

## Flow: Calendar Busy-Time, Selected Calendar, and Destination Calendar Behavior

### Docs Intent

The docs matrix marks Google Calendar, Outlook/Office 365 Calendar, Apple Calendar, CalDAV, Lark, Feishu, Zoho, Exchange, ICS feed, availability schedules, buffers, minimum notice, and booking limits as supported.

Calendar behavior has two distinct concepts:

- Selected calendars are read for busy-time conflicts.
- Destination calendars receive the created calendar event.

### REST API

Calendar controller:

- `cal.diy/apps/api/v2/src/platform/calendars/controllers/calendars.controller.ts`

Routes:

- `POST /v2/calendars/ics-feed/save`
- `GET /v2/calendars/ics-feed/check`
- `GET /v2/calendars/busy-times`
- `GET /v2/calendars`
- `GET /v2/calendars/:calendar/connect`
- `GET /v2/calendars/:calendar/save`
- `POST /v2/calendars/:calendar/credentials`
- `GET /v2/calendars/:calendar/check`
- `POST /v2/calendars/:calendar/disconnect`

Selected calendar controller:

- `cal.diy/apps/api/v2/src/modules/selected-calendars/controllers/selected-calendars.controller.ts`

Routes:

- `POST /v2/selected-calendars`
- `DELETE /v2/selected-calendars`

Destination calendar controller:

- `cal.diy/apps/api/v2/src/modules/destination-calendars/controllers/destination-calendars.controller.ts`

Route:

- `PUT /v2/destination-calendars`

Busy-time request shape:

- Requires date range, calendars to load, and a time zone parameter.
- Returns busy ranges gathered from selected calendar integrations.

Destination calendar request shape:

- Updates destination calendar integration/external id/delegation credential for the authenticated user.

### Web/tRPC

tRPC route source:

- `cal.diy/packages/trpc/server/routers/viewer/calendars/_router.tsx`

Procedures:

- `viewer.calendars.connectedCalendars`
- `viewer.calendars.setDestinationCalendar`
- `viewer.calendars.setDestinationReminder`

Other related tRPC route:

- `viewer.availability.calendarOverlay`

Web route source:

- `cal.diy/apps/web/app/(use-page-wrapper)/settings/(settings-layout)/my-account/calendars/page.tsx`

### Business-Rule Seams

- `cal.diy/packages/features/busyTimes/services/getBusyTimes.ts`
- `cal.diy/packages/features/busyTimes/lib/getBusyTimesFromLimits.ts`
- `cal.diy/packages/features/selectedCalendar/repositories/SelectedCalendarRepository.ts`
- `cal.diy/packages/lib/server/withSelectedCalendars.ts`
- `cal.diy/packages/features/users/repositories/UserRepository.ts`
- `cal.diy/packages/features/CalendarEventBuilder.test.ts`
- `cal.diy/apps/api/v2/src/platform/calendars/services/calendars.service.ts`
- `cal.diy/apps/api/v2/src/modules/selected-calendars/services/selected-calendars.service.ts`
- `cal.diy/apps/api/v2/src/modules/destination-calendars/services/destination-calendars.service.ts`

`withSelectedCalendars` splits all selected calendars into all calendars and user-level calendars. Event-type-level selected calendars are part of the full set.

### Database Entities

- `SelectedCalendar`
- `DestinationCalendar`
- `Credential`
- `EventType`
- `Booking`
- `BookingReference`
- `Schedule`
- `Availability`

### Nerova Port Decision

Copy after Service/Event Types + Availability:

- selected calendars as conflict sources
- destination calendar as write target
- event-type-level selected calendar override
- calendar busy-time abstraction
- provider credential model

Simplify:

- start with one connected calendar provider if needed for MVP
- allow manual availability without connected calendars
- model external credentials in Nerova integration infrastructure, not Cal.diy app-store tables

Defer:

- ICS feed save/check
- CalDAV/Apple/Lark/Feishu/Zoho/Exchange
- calendar subscriptions and cache refresh
- destination reminders
- event CRUD via unified calendar API

### API v2 Gaps

REST has calendar endpoints, but slot and booking correctness depends on package-level busy-time merging, selected-calendar user shape, calendar adapters, and destination-calendar event creation logic.

The first Nerova slice should model selected and destination calendars explicitly even if provider OAuth is deferred.

## Flow: Webhooks and Integration Side Effects

### Docs Intent

The docs matrix marks webhooks, Zapier, n8n, Make, Pipedream, CRM integrations, messaging integrations, AI agents, and analytics apps as supported. This is part of the "many integrations" surface Nerova wants long term.

### REST API

User webhook controller:

- `cal.diy/apps/api/v2/src/modules/webhooks/controllers/webhooks.controller.ts`

Routes:

- `POST /v2/webhooks`
- `PATCH /v2/webhooks/:webhookId`
- `GET /v2/webhooks/:webhookId`
- `GET /v2/webhooks`
- `DELETE /v2/webhooks/:webhookId`

Event-type webhook controller:

- `cal.diy/apps/api/v2/src/modules/event-types/controllers/event-types-webhooks.controller.ts`

Routes:

- `POST /v2/event-types/:eventTypeId/webhooks`
- `PATCH /v2/event-types/:eventTypeId/webhooks/:webhookId`
- `GET /v2/event-types/:eventTypeId/webhooks/:webhookId`
- `GET /v2/event-types/:eventTypeId/webhooks`
- `DELETE /v2/event-types/:eventTypeId/webhooks/:webhookId`
- `DELETE /v2/event-types/:eventTypeId/webhooks`

### Web/tRPC

tRPC route source:

- `cal.diy/packages/trpc/server/routers/viewer/webhook/_router.tsx`

Procedures include create, edit, get, list, delete, get by viewer, and test trigger.

### Business-Rule Seams

- `cal.diy/packages/features/webhooks/lib/WebhookService.ts`
- `cal.diy/packages/features/webhooks/lib/service/BookingWebhookService.ts`
- `cal.diy/packages/features/webhooks/lib/sendPayload.ts`
- `cal.diy/packages/features/webhooks/lib/sendOrSchedulePayload.ts`
- `cal.diy/packages/features/webhooks/lib/schedulePayload.ts`
- `cal.diy/packages/features/webhooks/lib/tasker/WebhookTasker.ts`
- `cal.diy/packages/lib/server/service/BookingWebhookFactory.ts`

### Database Entities

- `Webhook`
- `WebhookScheduledTriggers`
- `EventType`
- `Booking`
- `BookingReference`

### Nerova Port Decision

Defer until booking lifecycle exists.

Copy:

- webhook subscription model
- event-type-scoped webhooks
- booking lifecycle payload concepts

Replace:

- use Nerova's integration/event infrastructure rather than Cal.diy tasker directly

Simplify:

- first internal event stream can be local domain events; external webhooks can follow once booking state is stable

### API v2 Gaps

Webhook CRUD is exposed by REST, but the payload contract is produced by package services during booking side effects. Porting CRUD without lifecycle events gives a false sense of completion.

## Cross-Cutting API v2 Insufficiencies

API v2 is useful, but it is not a complete architecture contract for Nerova. Gaps found in source:

- The Cal.diy dashboard uses tRPC for event-type grouping, editor mutations, schedule duplicate/update flows, slots, bookings, calendars, apps, Cal Video, and webhooks.
- Public booking pages are Next routes with server loaders and Booker components. They are not REST-only.
- Core business rules live in `packages/features` and `packages/platform/libraries`, not in controllers.
- Slot correctness depends on availability packages, busy-time packages, booking limits, selected calendars, and slot reservation storage.
- Booking correctness depends on notification taskers, webhooks, calendar adapters, video adapters, payment hooks, cancellation policy, reschedule policy, confirmation policy, guests, attendees, and audit behavior.
- Cal.diy docs say teams/orgs are not supported, but source includes Cal.com team/org remnants. Nerova must not import those as product truth.
- The app-store surface cannot be reproduced by calling one REST API. App metadata, app credentials, default conferencing, dependency checks, and location options are dashboard and package-driven.
- OAuth/platform APIs are present, but the user experience goal is one Nerova dashboard. OAuth is not enough to embed all Cal.com or Cal.diy management flows.

## Nerova Acceptance Contract

Every ported scheduling slice must be accepted against the behavior sources in this map, not against a directory checklist.

For Service/Event Types + Availability, acceptance means:

- A tenant provider can create, edit, hide, and delete a service.
- A service has stable title, slug, description, duration, optional variable durations, locations, schedule binding, buffers, minimum notice, booking window, booking count limits, and cancellation/reschedule policy fields.
- A provider has one default availability schedule.
- A provider can create named schedules with weekly availability windows and date overrides.
- Time zones are explicit and used for interpreting schedule windows.
- Event types can point to a specific schedule or use the provider default schedule.
- The model leaves room for selected calendar and destination calendar references even if calendar OAuth comes in the next slice.
- Team, round-robin, managed, recurring, seated, paid, travel, and out-of-office behavior is intentionally deferred rather than accidentally half-ported.

## First Implementation Slice

Build **Service/Event Types + Availability** in PlatformPlatform/Nerova first.

Minimum backend model:

- `Service` or `EventType` aggregate owned by a Nerova tenant and provider/user.
- `AvailabilitySchedule` aggregate with `Name`, `TimeZone`, `IsDefault`.
- `AvailabilityWindow` value objects for weekly windows.
- `AvailabilityOverride` value objects for date overrides.
- Service fields for duration, slug, title, description, locations, buffers, minimum notice, booking window, booking limits, hidden state, and schedule reference.

Minimum frontend workflow:

- service list
- create service
- edit service basics
- edit service availability binding
- schedule list
- create/edit schedule weekly hours
- create/edit date overrides

Minimum tests:

- tenant authorization around service and schedule ownership
- default schedule behavior
- slug uniqueness inside tenant/provider scope
- schedule time-zone persistence
- weekly availability validation
- date override validation
- service schedule binding
- deferred-feature tests asserting unsupported options are rejected or absent

The next planning document should use this map to break the first slice into PlatformPlatform vertical slices, but this milestone itself stops at documentation.

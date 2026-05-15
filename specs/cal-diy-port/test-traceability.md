# Test Traceability

## Summary

- Inventoried source test/spec/e2e related files: 534
- Classification values:
  - `port`: translate behavior into Nerova unit/integration/API/E2E tests.
  - `replace`: source behavior is still required, but the implementation surface changes because of Nerova auth or WhatsApp booking.
  - `adapt`: use as parity or support reference for Nerova architecture/UI wrappers.
  - `defer`: out of Solo v1 unless a later accepted slice depends on it.

## Required Nerova Test Gates

- API tests: endpoint contracts, auth, permissions, validation, idempotency, error shapes.
- Domain tests: slot generation, busy-time merge, connector failures, booking lifecycle, retries, timezone behavior.
- Frontend tests: admin UI modules, connector setup states, responsive layouts, loading/error/empty states.
- WhatsApp tests: verification, Flow data exchange, version mismatch, duplicates, invalid connector state, stale slot rejection, happy path.
- E2E tests: Solo onboarding, event type setup, connector setup, WhatsApp booking, reschedule/cancel, non-Solo denial.
- Guardian gate: build, format, lint, backend tests, frontend checks, smoke E2E, reviewer approvals.

## Per-File Matrix

| Path | Class | Reason |
| --- | --- | --- |
| cal.diy/__checks__/csp-login.spec.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/api/v2/src/lib/inputs/capitalize-timezone.spec.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/api/v2/src/lib/is-origin-allowed/is-origin-allowed.spec.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/apps/api/v2/src/lib/pagination/pagination.spec.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/apps/api/v2/src/modules/auth/guards/or-guard/or.guard.spec.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/api/v2/src/modules/auth/guards/permissions/permissions.guard.spec.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/api/v2/src/modules/cal-unified-calendars/controllers/cal-unified-calendars.controller.spec.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/api/v2/src/modules/cal-unified-calendars/pipes/get-calendar-event-details-output-pipe.spec.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/api/v2/src/modules/cal-unified-calendars/pipes/google-calendar-event-input.pipe.spec.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/api/v2/src/modules/cal-unified-calendars/services/google-calendar.service.spec.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/api/v2/src/modules/cal-unified-calendars/services/unified-calendars-freebusy.integration.spec.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/api/v2/src/modules/cal-unified-calendars/services/unified-calendars-freebusy.service.spec.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/api/v2/src/modules/slots/slots-2024-09-04/services/slots.service.spec.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/api/v2/src/modules/users/validators/avatarValidator.spec.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/apps/api/v2/src/modules/webhooks/utils/validate-webhook-url.spec.ts | port | Notification/background/webhook semantics required. |
| cal.diy/apps/api/v2/src/platform/event-types/event-types_2024_06_14/services/output-event-types.service.spec.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/api/v2/src/platform/event-types/event-types_2024_06_14/transformers/api-to-internal/api-to-internal.spec.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/api/v2/src/platform/event-types/event-types_2024_06_14/transformers/internal-to-api/internal-to-api.spec.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/web/app/api/auth/oauth/token/__tests__/route.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/app/api/auth/signup/handlers/calcomSignupHandler.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/app/api/auth/signup/handlers/selfHostedHandler.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/app/api/cron/calendar-subscriptions-cleanup/__tests__/route.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/web/app/api/cron/calendar-subscriptions/__tests__/route.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/web/app/api/cron/selected-calendars/__tests__/cron.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/apps/web/app/api/defaultResponderForAppDir.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/app/api/link/__tests__/route.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/app/api/social/og/image/__tests__/route.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/app/api/verify-booking-token/__tests__/route.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/app/api/video/recording/__tests__/route.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/app/api/webhooks/calendar-subscription/[provider]/__tests__/route.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/web/app/WithEmbedSSR.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/apps/web/components/apps/InstallAppButtonChild.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/components/booking/__tests__/CancelBooking.cancellationFee.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/components/booking/actions/bookingActions.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/components/dialog/__tests__/EditLocationDialog.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/components/phone-input/PhoneInput.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/lib/__tests__/getThemeProviderProps.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/lib/apps/[slug]/__tests__/parseFrontmatter.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/lib/buildNonce.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/lib/daily-webhook/tests/recorded-daily-video.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/apps/web/lib/handleOrgRedirect.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/lib/pages/document/_applyThemeForDocument.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/lib/withEmbedSsr.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/apps/web/modules/apps/components/appCard.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/bookings/components/Booker.test.tsx | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/apps/web/modules/bookings/components/BookingDetailsSheet.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/bookings/components/DatePicker.test.tsx | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/apps/web/modules/bookings/hooks/useActiveFiltersValidator.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/bookings/lib/bookingSheetKeyboardHandler.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/data-table/components/filters/ColumnVisibilityButton.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/event-types/components/tabs/advanced/FormBuilder.test.tsx | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/web/modules/form-builder/components/FormBuilderField.test.tsx | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/web/modules/onboarding/hooks/__tests__/useSubmitOnboarding.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/schedules/components/date-override-list.test.tsx | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/modules/shell/user-dropdown/UserDropdown.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/signup-view.test.tsx | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/modules/timezone/components/TimezoneSelect.test.tsx | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/modules/users/components/AdminPasswordBanner.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/users/lib/UserListTableUtils.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/users/views/users-public-view.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/modules/videos/__tests__/cal-video-premium-features.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/pages/api/book/recurring-event.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/ab-tests-redirect.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/app-list-card.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/app-router-not-found.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/apps/analytics/analyticsApps.e2e.ts | defer | Analytics connectors are out of v1 connector scope. |
| cal.diy/apps/web/playwright/apps/conferencing/conferencingApps.e2e.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/apps/web/playwright/apps/conferencing/types.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/auth/delete-account.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/auth/forgot-password.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/availability.e2e.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/playwright/booking-confirm-reject.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/booking-duplicate-api-calls.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/booking-limits.e2e.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/playwright/booking-pages.e2e.ts | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/apps/web/playwright/booking-phone-autofill.e2e.ts | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/apps/web/playwright/booking-seats.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/booking-sheet-keyboard.e2e.ts | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/apps/web/playwright/cancellation-fee-warning.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/change-password.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/change-theme.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/change-username.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/duration-limits.e2e.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/playwright/dynamic-booking-pages.e2e.ts | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/apps/web/playwright/embed-code-generator.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/apps/web/playwright/event-types.e2e.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/web/playwright/eventType/availability-tab.e2e.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/playwright/eventType/limit-tab.e2e.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/web/playwright/filter-helpers.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/apps.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/bookings.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/cal.png | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/cal2.png | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/emails.ts | port | Notification/background/webhook semantics required. |
| cal.diy/apps/web/playwright/fixtures/embeds.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/apps/web/playwright/fixtures/eventTypes.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/apps/web/playwright/fixtures/features.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/orgs.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/payments.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/apps/web/playwright/fixtures/regularBookings.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/servers.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/types.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/users.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/fixtures/webhooks.ts | port | Notification/background/webhook semantics required. |
| cal.diy/apps/web/playwright/hash-my-url.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/hide-duration-selector.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/i18n-routing.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/icons.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/icons.e2e.ts-snapshots/icons--calcom-web-linux.png | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/integrations.e2e.ts-snapshots/webhookResponse--calcom-web.txt | port | Notification/background/webhook semantics required. |
| cal.diy/apps/web/playwright/lib/chart-helpers.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/lib/fixtures.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/lib/loadJSON.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/lib/localize.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/lib/next-server.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/lib/pageObject.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/lib/teardown.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/lib/testUtils.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/locale.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/login.2fa.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/login.api.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/login.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/login.oauth.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/manage-booking-questions.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/oauth-provider.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/oauth/oauth-authorize-approval-status.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/oauth/oauth-client-admin.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/oauth/oauth-client-helpers.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/oauth/oauth-client-owner-crud.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/oauth/oauth-refresh-tokens.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/onboarding.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/out-of-office.e2e.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/playwright/overlay-calendar.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/payment-apps.e2e.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/apps/web/playwright/payment.e2e.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/apps/web/playwright/profile.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/reschedule.e2e.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/playwright/settings-admin.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/settings/upload-avatar.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/playwright/signup.e2e.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/apps/web/playwright/trial.e2e.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/apps/web/playwright/webhook.e2e.ts | port | Notification/background/webhook semantics required. |
| cal.diy/apps/web/playwright/wipe-my-cal.e2e.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/proxy.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/test/.env.test.example | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/test/handlers/requestReschedule.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/availabilityAsString.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/checkBookingLimits.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/test/lib/checkDurationLimits.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/test/lib/getAvailabilityFromSchedule.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getSchedule.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getSchedule/calendarEvents.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getSchedule/delegation-credential.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getSchedule/futureLimit.timezone.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getSchedule/restrictionSchedule.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getSchedule/selectedSlots.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getTimezone.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/getWorkingHours.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/apps/web/test/lib/next-config.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/test/lib/pagesAndRewritePaths.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/apps/web/test/lib/parseZone.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store-cli/src/validateCreateAppFlags.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store/_components/eventTypeAppCardInterface.test.tsx | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/app-store/_utils/bulkUpdateEventsToDefaultLocation.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store/_utils/getBulkEventTypes.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/app-store/_utils/getDefaultLocations.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store/_utils/oauth/OAuthManager.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/app-store/_utils/oauth/updateProfilePhotoMicrosoft.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/app-store/_utils/payments/handlePaymentSuccess.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/app-store/_utils/validateAppKeys.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store/AppDependencyComponent.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store/BookingPageTagManager.test.tsx | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store/closecom/test/lib/CalendarService.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/googlecalendar/lib/__tests__/CalendarService.auth.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/app-store/googlecalendar/lib/__tests__/CalendarService.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/app-store/googlecalendar/lib/__tests__/utils.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/app-store/googlecalendar/tests/google-calendar.e2e.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/app-store/googlecalendar/tests/testUtils.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/app-store/hubspot/lib/CrmService.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/office365video/lib/VideoApiAdapter.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/app-store/salesforce/lib/__tests__/CrmService.integration.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/salesforce/lib/__tests__/getSalesforceTokenLifetime.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/salesforce/lib/__tests__/salesforceMock.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/salesforce/lib/CrmService.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/salesforce/lib/graphql/__tests__/SalesforceGraphQLClient.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/salesforce/lib/graphql/__tests__/urqlMock.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/salesforce/lib/utils/__tests__/getAllPossibleWebsiteValuesFromEmailDomain.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/salesforce/lib/utils/__tests__/getDominantAccountId.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/app-store/stripepayment/api/__tests__/paymentCallback.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/app-store/stripepayment/api/__tests__/portal.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/app-store/stripepayment/lib/repositories/VerificationTokenRepository.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/app-store/stripepayment/lib/VerificationTokenService.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/app-store/stripepayment/pages/setup/__tests__/_getServerSideProps.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/app-store/tests/__mocks__/OAuthManager.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/app-store/utils.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/app-store/zoomvideo/lib/VideoApiAdapter.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/emails/email-manager.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/emails/lib/generateIcsString.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/emails/lib/getICalUID.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/emails/templates/admin-oauth-client-notification.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/emails/templates/oauth-client-approved-notification.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/emails/templates/oauth-client-rejected-notification.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/embeds/embed-core/playwright/lib/pages/bookingSuccessPage.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/playwright/lib/testUtils.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/playwright/tests/action-based.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/playwright/tests/embed-pages.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/playwright/tests/inline.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/playwright/tests/namespacing.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/playwright/tests/preview.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/playwright/tests/two-step-slot-selection.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/__tests__/embed-iframe-methods.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/__tests__/embed-iframe.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/__tests__/utils.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/embed-iframe/__tests__/isLinkReady.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/embed-iframe/__tests__/react-hooks.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/embed-iframe/__tests__/test-utils.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/embed-iframe/__tests__/utils.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/embed.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/EmbedElement.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/lib/domUtils.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-core/src/ModalBox/ModalBox.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-react/playwright/tests/basic.e2e.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/embeds/embed-react/test/packaged/api.test.ts | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/features/api-keys-legacy/api-keys/lib/autoLock.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/auth/lib/getServerSession.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/lib/identityProviders.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/lib/next-auth-options.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/lib/outlook.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/signup/handlers/__tests__/mocks/next.mocks.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/signup/handlers/__tests__/mocks/prisma.mocks.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/signup/handlers/__tests__/mocks/signup.factories.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/signup/handlers/__tests__/p2002.test-suite.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/signup/lib/fetchSignup.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/auth/signup/utils/getOrgUsernameFromEmail.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/features/availability/lib/calculateHolidayBlockedDates.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/availability/lib/detectEventTypeScheduleForUser.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/availability/lib/getAggregatedAvailability/date-range-utils/filterRedundantDateRanges.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/availability/lib/getAggregatedAvailability/date-range-utils/mergeOverlappingDateRanges.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/availability/lib/getAggregatedAvailability/getAggregatedAvailability.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/availability/lib/getUserAvailabilityIncludingBusyTimesFromLimits.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/AcceptedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/AttendeeAddedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/AttendeeRemovedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/CancelledAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/contractVerification.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/CreatedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/LocationChangedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/NoShowUpdatedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/ReassignmentAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/RejectedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/RescheduledAuditActionService.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/RescheduleRequestedAuditActionService.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/SeatBookedAuditActionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/actions/__tests__/SeatRescheduledAuditActionService.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/booking-audit/lib/service/__tests__/BookingAuditAccessService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/service/__tests__/BookingAuditViewerService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/service/__tests__/EnrichmentDataStore.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/booking-audit/lib/service/__tests__/integration-utils.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookingReport/repositories/PrismaBookingReportRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/__tests__/test-utils.tsx | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/__tests__/utils/isSlotEquivalent.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/hooks/useInitialFormValues.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/utils/areDifferentValidMonths.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/utils/getPrefetchMonthCount.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/utils/isFeatureEnabledForVisitor.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/utils/isMonthChange.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/utils/isMonthViewPrefetchEnabled.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/utils/isPrefetchNextMonthEnabled.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/Booker/utils/isTimeslotAvailable.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/bookingSuccessRedirect.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/buildEventUrlFromBooking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/conflictChecker/checkForConflicts.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/EventManager.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/getAllCredentialsForUsersOnEvent/getAllCredentials.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/getAssignmentReasonCategory.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/getBookingResponsesSchema.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/getCalendarLinks.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/getLuckyUser.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleCancelBooking/test/handleCancelBooking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/getBookingData.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/global-booking-limits.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/booking-flags.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/booking-limits.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/booking-validations.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/buildDryRunBooking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/complex-schedules.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/computeTeamData.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/date-overrides.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/dynamic-group-booking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/email-verification-booking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/fresh-booking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/getLocationValueForDb.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/managed-event-type-booking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/per-host-locations.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/post-booking-handling.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/reschedule.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/bookings/lib/handleNewBooking/test/webhook-producer-booking-requested.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/bookings/lib/handlePayment.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/features/bookings/lib/handleSeats/test/handleSeats.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/lib/payment/handleNoShowFee.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/features/bookings/lib/payment/processNoShowFeeOnCancellation.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/features/bookings/lib/payment/processPaymentRefund.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/features/bookings/lib/payment/shouldChargeNoShowCancellationFee.test.ts | defer | Payments/subscriptions are out of Solo scheduling v1 unless shared infrastructure is needed. |
| cal.diy/packages/features/bookings/lib/reschedule/determineReschedulePreventionRedirect.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/bookings/lib/service/RecurringBookingService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/repositories/BookingRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/repositories/WrongAssignmentReportRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bookings/services/BookingAccessService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/bot-detection/BotDetectionService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/busyTimes/services/getBusyTimes.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/cache/decorators/__tests__/Memoize.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/cache/decorators/__tests__/Unmemoize.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/calendar-subscription/adapters/__tests__/AdaptersFactory.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/adapters/__tests__/GoogleCalendarSubscriptionAdapter.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/adapters/__tests__/Office365CalendarSubscriptionAdapter.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/lib/__tests__/CalendarSubscriptionService.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/lib/cache/__tests__/CalendarCacheEventRepository.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/lib/cache/__tests__/CalendarCacheEventService.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/lib/cache/__tests__/CalendarCacheWrapper.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/lib/sync/__tests__/CalendarSyncService.integration.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/calendar-subscription/lib/sync/__tests__/CalendarSyncService.test.ts | port | Workforce connector behavior required for Solo. |
| cal.diy/packages/features/CalendarEventBuilder.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/calendars/__tests__/DatePicker.test.tsx | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/packages/features/calendars/__tests__/NoAvailabilityDialog.test.tsx | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/calendars/lib/CalendarManager.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/calendars/lib/getAvailableDatesInMonth.timezone.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/calendars/lib/getCalendarsEvents.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/calendars/lib/timezone-conversion.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/calendars/weeklyview/utils/overlap.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/credentials/deleteCredential.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/credentials/services/CredentialAccessService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/credits/repositories/CreditsRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/crmManager/crmManager.test.ts | defer | Non-workforce connector outside v1 scope. |
| cal.diy/packages/features/data-table/__tests__/filterSegments/create.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/data-table/__tests__/filterSegments/delete.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/data-table/__tests__/filterSegments/get.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/data-table/__tests__/filterSegments/update.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/data-table/lib/__tests__/preserveLocalTime.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/data-table/lib/__tests__/server.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/data-table/lib/dateRange.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/embed/lib/getApiName.test.tsx | defer | Public web embed/booker surface is replaced by WhatsApp Flow. |
| cal.diy/packages/features/eventtypes/lib/eventNaming.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/features/eventtypes/lib/isCurrentlyAvailable.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/features/eventtypes/repositories/__tests__/EventTypeRepository.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/features/eventtypes/service/EventTypeService.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/features/eventTypeTranslation/repositories/EventTypeTranslationRepository.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/features/feature-opt-in/lib/applyAutoOptIn.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/feature-opt-in/lib/computeEffectiveState.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/feature-opt-in/services/FeatureOptInService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/flags/operations/check-if-user-has-feature.controller.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/flags/operations/check-if-user-has-feature.use-case.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/flags/repositories/__tests__/FeatureRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/flags/repositories/__tests__/TeamFeatureRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/flags/repositories/__tests__/UserFeatureRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/form-builder/useShouldBeDisabledDueToPrefill.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/get-cal-video-reference.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/handleMarkNoShow.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/holidays/repositories/HolidayRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/host/repositories/HostRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/host/services/EventTypeHostService.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/features/profile/lib/getBranding.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/profile/lib/hideBranding.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/profile/repositories/ProfileRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/schedules/components/parse-time-string.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/schedules/hooks/useTimesForSchedule.timezone.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/schedules/lib/date-ranges.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/schedules/lib/slots.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/schedules/repositories/ScheduleRepository.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/selectedCalendar/repositories/SelectedCalendarRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/slots/handleNotificationWhenNoSlots.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/tasker/tasks/crm/__tests__/createCRMEvent.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/tasker/tasks/triggerNoShow/triggerGuestNoShow.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/tasker/tasks/triggerNoShow/triggerHostNoShow.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/translation/services/TranslationService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/url-shortener/__tests__/DubShortener.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/url-shortener/__tests__/NoopShortener.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/url-shortener/__tests__/SinkClient.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/url-shortener/__tests__/SinkShortener.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/url-shortener/__tests__/UrlShortenerFactory.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/users/lib/getRoutedUsers.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/users/repositories/UserRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/users/services/userCreationService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/lib/freeEmailDomainCheck/checkIfFreeEmailDomain.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/lib/repository/GlobalWatchlistRepository.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/lib/service/AdminWatchlistOperationsService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/lib/service/GlobalBlockingService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/lib/service/OrganizationBlockingService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/lib/service/WatchlistService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/lib/utils/normalization.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/operations/check-if-email-in-watchlist.controller.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/operations/check-if-users-are-blocked.controller.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/operations/check-user-blocking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/operations/filter-blocked-hosts.controller.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/operations/filter-blocked-users.controller.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/watchlist/operations/list-all-system-entries.controller.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/features/webhooks/lib/__tests__/consumer/triggers/booking-requested.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/__tests__/consumer/WebhookTaskConsumer.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/__tests__/producer/WebhookTaskerProducerService.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/__tests__/webhookDelivery.integration-test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/factory/base/BaseBookingPayloadBuilder.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/factory/base/BaseMeetingPayloadBuilder.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/factory/base/BaseOOOPayloadBuilder.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/webhooks/lib/factory/base/BaseRecordingPayloadBuilder.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/factory/versioned/PayloadBuilderFactory.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/factory/versioned/registry.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/factory/versioned/v2021-10-20/BookingPayloadBuilder.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/handleWebhookScheduledTriggers.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/features/webhooks/lib/sendPayload.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/service/__tests__/fixtures.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/service/__tests__/WebhookTaskerProducerService.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/service/WebhookNotificationHandler.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/service/WebhookService.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/tasker/WebhookTasker.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/test/webhooks.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/features/webhooks/lib/WebhookService.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/i18n/next-i18next.config.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/i18n/server.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/lib/__tests__/buildCalEventFromBooking.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/__tests__/timeShift.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/array.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/bookings/routing/utils.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/CalendarService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/CalEventParser.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/checkRateLimitAndThrowError.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/crypto.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/cva/cva.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/dateTimeFormatter.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/dayjs/formatToLocalizedTimezone.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/lib/dayjs/stringToDayjs.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/getBrandColours.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/getIP.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/getReplyToEmail.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/getReplyToHeader.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/getSafeRedirectUrl.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/getValidRhfFieldName.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/holidays/HolidayService.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/hooks/useCompatSearchParams.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/hooks/useParamsWithFallback.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/hooks/useUserAgentData.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/isOutOfBounds.timezone.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/lib/OgImages.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/pkce.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/random.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/redactSensitiveData.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/server/avatar.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/server/defaultHandler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/server/defaultResponder.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/server/getServerErrorFromUnknown.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/server/PiiHasher.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/server/service/__tests__/BookingWebhookFactory.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/lib/server/updateUserAvatarUrl.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/server/username.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/slugify.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/ssrfProtection.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/tasker/Tasker.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/lib/test/CalEventParser.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/text.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/tracing/index.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/tsconfig.test.json | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/weekday.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/lib/weekstart.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/platform/atoms/event-types/__tests__/EventTypeListItem.test.tsx | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/platform/atoms/event-types/__tests__/formatEventTypeDuration.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/platform/atoms/event-types/hooks/useEventTypeForm.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/platform/examples/base/tests/availability-settings-atom/availability-settings-atom.e2e.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/platform/examples/base/tests/booker-atom/booker-atom.e2e.ts | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/platform/examples/base/tests/connect-atoms/apple-connect.e2e.ts | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/platform/examples/base/tests/create-event-type-atom/create-event-type.e2e.ts | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/platform/examples/base/tests/create-team-event-type-atom/create-team-event-type.e2e.ts | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/platform/utils/tests/permissions.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/prisma/extensions/disallow-undefined-delete-update-many.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/prisma/zod-utils.test.ts | adapt | Support, UI, or infrastructure parity to review in the owning implementation slice. |
| cal.diy/packages/sms/test/sms-manager.test.ts | port | Notification/background/webhook semantics required. |
| cal.diy/packages/trpc/server/createNextApiHandler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/errorFormatter.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/lib/toTRPCError.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/loggedInViewer/connectAndJoin.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/loggedInViewer/unlinkConnectedAccount.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/admin/lockUserAccount.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/apps/appById.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/availability/schedule/getAllSchedulesByUserId.handler.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/trpc/server/routers/viewer/bookings/confirm.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/bookings/editLocation.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/bookings/get.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/bookings/reportBooking.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/bookings/reportWrongAssignment.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/calendars/setDestinationCalendar.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/eventTypes/__tests__/getUserEventGroups.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/trpc/server/routers/viewer/eventTypes/__tests__/util.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/trpc/server/routers/viewer/eventTypes/heavy/duplicate.handler.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/trpc/server/routers/viewer/eventTypes/heavy/update.handler.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/trpc/server/routers/viewer/eventTypes/utils/EventTypeGroupFilter.test.ts | port | Event type/admin behavior required for Solo. |
| cal.diy/packages/trpc/server/routers/viewer/googleWorkspace/googleWorkspace.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/i18n/i18n.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/me/get.handler.test.ts | port | Business/API behavior source for .NET rewrite. |
| cal.diy/packages/trpc/server/routers/viewer/oAuth/generateAuthCode.handler.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/trpc/server/routers/viewer/oAuth/submitClientForReview.handler.test.ts | replace | Use as auth/credential intent only; Nerova account/auth architecture owns implementation. |
| cal.diy/packages/trpc/server/routers/viewer/ooo/outOfOfficeCreateOrUpdate.handler.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/trpc/server/routers/viewer/slots/getBusyTimesFromLimitsForUsers.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/trpc/server/routers/viewer/slots/reserveSlot.handler.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/trpc/server/routers/viewer/slots/util.test.ts | port | Scheduling-critical algorithm or edge case. |
| cal.diy/packages/ui/components/alert/alert.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/avatar/UserAvatar.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/badge/badge.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/breadcrumb/breadcrumb.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/button/button.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/card/card.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/credits/credits.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/dialog/dialog.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/editor/Editor.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/editor/nodes/VariableNode.test.ts | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/editor/plugins/AddVariablesDropdown.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/editor/plugins/AutoLinkPlugin.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/editor/plugins/CustomEnterKeyPlugin.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/editor/plugins/EditablePlugin.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/editor/plugins/ToolbarPlugin.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/errorBoundary/error-boundary.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/form/checkbox/Checkbox.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/form/color-picker/colorpicker.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/form/date-range-picker/dateRangeLogic.test.ts | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/form/datepicker/datepicker.test.tsx | replace | Use behavior as reference, but public booking UX becomes WhatsApp Flow and admin booking views use Nerova UI. |
| cal.diy/packages/ui/components/form/inputs/input.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/form/select/select.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/form/step/steps.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/form/wizard/wizardForm.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/layout/shellSubHeading.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/list/list.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/navigation/tabs/navigation.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/organization-banner/OrgBanner.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/scrollable/scrollablearea.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/table/table.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/toast/toast.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/TokenHandler/token-handler.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |
| cal.diy/packages/ui/components/top-banner/topBanner.test.tsx | adapt | Atom/UI behavior reference; implement through Nerova wrappers without editing shared primitives. |

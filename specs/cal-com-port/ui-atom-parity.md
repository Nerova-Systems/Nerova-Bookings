# UI And Atom Parity

Source policy: Cal.com is primary; Cal.diy is secondary reference only.

## Policy

Cal.com atoms and authenticated admin layouts are the UX oracle for the scheduling port. Nerova keeps its own shell, routing, translations, generated clients, and `@repo/ui` styling primitives.

Do not edit shared styling primitives unless a separate approved task explicitly allows it. Create scheduling-local adapters in `application/main/WebApp` when Cal.com atom behavior needs Nerova-specific data or API integration.

## Required Parity Areas

- Getting started and scheduling onboarding.
- Calendar connection setup.
- Video connection setup.
- Event type list, create, duplicate, edit, disable, and delete.
- Event type advanced fields and form builder behavior.
- Availability schedules, weekly hours, date overrides, timezone selection, out-of-office, and holiday behavior where included.
- Booking list, booking detail sheet, status tabs, filters, reschedule, cancel, confirm, reject, and no-show admin actions.
- App-store list, connector detail, install, uninstall, reconnect, error, loading, empty, and disabled states.
- Webhooks and background task admin surfaces where included in v1.

## Public Booking Exception

Solo public booking is WhatsApp Flow only. Cal.com public web booking pages, embed pages, and public booker components are behavior references for scheduling and booking semantics, not web UI deliverables.

## Evidence Requirements

Frontend implementation tasks must attach or reference:

- Cal.com source components and tests.
- Nerova target components and routes.
- Desktop and mobile screenshots.
- Loading, empty, error, disabled, installed, uninstalled, and reconnect states where relevant.
- Accepted deviations.

Accepted deviations are limited to Nerova shell/branding, WhatsApp public booking replacement, and `@repo/ui` primitive styling.

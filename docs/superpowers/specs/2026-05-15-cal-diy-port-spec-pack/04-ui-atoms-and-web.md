# UI, Atoms, And Web Port Spec

This document defines how to port Cal.diy's UI and atom surfaces without importing its runtime framework or visual styling wholesale.

## UI Port Policy

- Preserve Cal.diy layouts, information hierarchy, workflow order, and state behavior wherever the feature is included.
- Restyle into Nerova conventions using `@repo/ui`, existing app shell, Lingui translations, TanStack Query, generated OpenAPI clients, and route-level error/loading boundaries.
- Do not import Radix, BaseUI directly, Next components, or Cal.diy packages into production code.
- Do not create public Solo web booking pages. Use Cal.diy Booker UI as a state-machine and layout reference for WhatsApp Flow behavior, admin previews, and future web fallback.
- All icon-only buttons need tooltips. All dialogs and side panes need Nerova tracking titles.

## Web Route Inventory

Port or replace these route families:

- Public booking wrapper: profile, event booking, embed, private link, booking success, booking detail, cancel. Replaced for Solo customer booking by WhatsApp Flow; keep behavior as reference.
- Main navigation: availability, booking detail/logs, bookings by status, event types, members. Port availability, bookings, event types. Members deferred for Solo.
- Apps: homepage, categories, installed, installation, app setup. Port with filtered v1 connector catalog.
- Auth: login, logout, signup, password reset, verification, OAuth authorize. Replace with account SCS.
- Onboarding/getting started: port as Solo scheduling setup flow.
- Settings: calendar settings, conferencing settings, profile/general appearance where scheduling requires, developer webhooks later. Replace account/security settings with existing account SCS.
- Video pages: port only if conferencing provider UX requires in-app video entry or fallback pages.
- Payment routes: defer Cal payment behavior.

## Web Module Inventory

| Module | UI obligation |
| --- | --- |
| `apps` | App-store grid/list, category filters, installed apps, setup flow, dependency warnings, connection states. |
| `availability` | Availability landing and schedule access. |
| `bookings` | Booking list, filter state, detail sheet/page, actions, date picker, Booker state reference, admin keyboard behavior. |
| `calendars` | Connected calendar list, selected calendars, destination calendar, no-availability warnings. |
| `data-table` | Port table/filter/sort/column behavior into `@repo/ui` table components. |
| `event-types` | Event type list, editor shell, tabs, advanced settings, availability binding, limits, booking fields, location settings. |
| `form-builder` | Booking question editor and field validation. |
| `onboarding` | Solo setup sequence for profile, availability, first service, connector. |
| `schedules` | Schedule editor, weekly hours, date overrides. |
| `settings` | Relevant scheduling settings only. |
| `shell` | Navigation/layout reference adapted to Nerova shell. |
| `timezone` | Timezone controls and conversion display. |
| `troubleshooter` | Availability/calendar diagnostics. |
| `users` | Profile/staff references only; account ownership remains elsewhere. |
| `videos` | Conferencing pages if provider scope requires. |
| `webhooks` | Developer webhook UI after lifecycle events exist. |

## Cal.diy Coss UI Components

Map these atom components into existing `@repo/ui` components or add missing Nerova components through the existing ShadCN/BaseUI path:

`accordion`, `alert-dialog`, `alert`, `autocomplete`, `avatar`, `badge`, `breadcrumb`, `button`, `card`, `checkbox-group`, `checkbox`, `collapsible`, `combobox`, `command`, `dialog`, `empty`, `field`, `fieldset`, `form`, `frame`, `group`, `input-group`, `input`, `kbd`, `label`, `menu`, `meter`, `number-field`, `pagination`, `popover`, `preview-card`, `progress`, `radio-group`, `scroll-area`, `select`, `separator`, `sheet`, `sidebar`, `skeleton`, `slider`, `spinner`, `switch`, `table`, `tabs`, `textarea`, `toast`, `toggle-group`, `toggle`, `toolbar`, `tooltip`.

## Cal.diy UI Package Components

Map or reference these `@calcom/ui` component areas:

`__mocks__`, `address`, `alert`, `app-list-card`, `arrow-button`, `avatar`, `badge`, `breadcrumb`, `button`, `buttonGroup`, `calendar-switch`, `card`, `command`, `credits`, `dialog`, `disconnect-calendar-integration`, `divider`, `dropdown`, `editable-heading`, `editor`, `empty-screen`, `errorBoundary`, `file-uploader`, `filter-select`, `form`, `hover-card`, `icon`, `image-uploader`, `layout`, `list`, `logo`, `navigation`, `organization-banner`, `pagination`, `popover`, `progress-bar`, `radio`, `scrollable`, `section`, `segmented-control`, `sheet`, `skeleton`, `table`, `toast`, `TokenHandler`, `tooltip`, `top-banner`, `unpublished-entity`.

`__mocks__` is test support only and maps to Nerova frontend test fixtures when equivalent component behavior is ported.

## Existing Nerova UI Components

Use these existing `@repo/ui` components before adding anything new:

`Accordion`, `AddToHomescreen`, `Alert`, `AlertDialog`, `AppLayout`, `AspectRatio`, `Avatar`, `Badge`, `BannerPortal`, `Breadcrumb`, `Button`, `ButtonGroup`, `Calendar`, `Card`, `Chart`, `Checkbox`, `CheckboxField`, `Collapsible`, `Combobox`, `ComboboxField`, `Command`, `ContextMenu`, `DateField`, `DateInput`, `DatePicker`, `DateRangePicker`, `Dialog`, `DirtyDialog`, `DirtyDialogContext`, `Drawer`, `DropdownMenu`, `Dropzone`, `Empty`, `Field`, `Form`, `HoverCard`, `InlineFieldGroup`, `Input`, `InputGroup`, `InputOtp`, `InputOtpField`, `Item`, `Kbd`, `Label`, `LabelWithTooltip`, `Link`, `LinkCard`, `MarkdownRenderer`, `MultiSelect`, `NavigationMenu`, `NumberField`, `Pagination`, `Popover`, `Progress`, `RadioGroup`, `RadioGroupField`, `Resizable`, `ScrollArea`, `Select`, `SelectField`, `Separator`, `Sheet`, `Sidebar`, `SidePane`, `Skeleton`, `Slider`, `SliderField`, `Sonner`, `Spinner`, `Switch`, `SwitchField`, `Table`, `TablePagination`, `Tabs`, `TenantLogo`, `Textarea`, `TextAreaField`, `TextField`, `TimeField`, `TimeZonePicker`, `Toggle`, `ToggleGroup`, `Tooltip`, `UnsavedChangesAlertDialog`.

## Platform Atoms

Inventory every atom even when public web booking is replaced:

- `availability`: availability editor behavior.
- `booker`: public booking state machine, slot selection, form, confirmation, reservation, error states.
- `booker-embed`: embed behavior reference; customer-facing embed deferred.
- `cal-provider`: provider context and API bootstrap reference.
- `calendar-settings`: connected calendar settings.
- `calendar-view`: calendar display reference.
- `connect`: integration connect controls.
- `create-schedule`: schedule creation.
- `destination-calendar`: destination calendar selection.
- `event-types`: event type list/create/edit atoms.
- `hooks`: shared atom hooks.
- `list-schedules`: schedule list behavior.
- `selected-calendars`: selected calendar controls.
- `timezone`: timezone controls.
- `troubleshooter`: availability troubleshooting.

Supporting atom directories:

- `fonts`: reference only; use Nerova typography and asset rules.
- `kysely-types`: reject runtime use; source typing artifact only.
- `lib`: port or replace shared atom helpers as needed by included atoms.
- `prisma-types`: reject runtime use; data-shape reference only.
- `scripts`: reference generation/build behavior only.
- `src`: package entry/support source; classify files by the atom/helper they support.
- `static`: copy only assets required by included UI surfaces and retain license attribution.

## Booker Replacement Rules

The web Booker is not a Solo public route, but its behavior must be preserved in WhatsApp:

- Loading, selecting date, selecting time, booking form, booking pending, success, and error states map to Flow screens.
- Slot reservation and stale-slot errors are backend concepts, not UI-only concepts.
- Form defaults, hidden fields, custom input validation, attendee details, email/phone verification, and timezone display map to Flow payloads.
- Admin preview may reuse Booker-like components if helpful, but customer booking must enter through WhatsApp.

## UI Acceptance

An included UI surface is accepted only when:

- The Cal.diy source route/module/component is named in the implementation task.
- The Nerova route uses generated API clients and TanStack Query.
- All user-facing text is translatable.
- Loading, empty, pending, disabled, validation, and error states are implemented.
- Responsive layouts match the Cal.diy workflow density while following Nerova design rules.
- Public web booking routes are absent or denied for Solo customer booking.

# UI Parity — Cal.com EE/Teams/Organizations Port

_Single source of truth for Wave 4 UI porting decisions._
_Generated from: `cal.com` @ `cf2a55c` · Nerova `agents-branch-review-feedback`_

---

## 1. Nerova Primitives Inventory

All files in `application/shared-webapp/ui/components/`.

### Input
- `Button.tsx`, `ButtonGroup.tsx`
- `Checkbox.tsx`, `CheckboxField.tsx`
- `Combobox.tsx`, `ComboboxField.tsx`
- `DateField.tsx`, `DateInput.tsx`, `DatePicker.tsx`, `DateRangePicker.tsx`
- `Dropzone.tsx`
- `Input.tsx`, `InputGroup.tsx`, `InputOtp.tsx`, `InputOtpField.tsx`
- `MultiSelect.tsx`
- `NumberField.tsx`
- `RadioGroup.tsx`, `RadioGroupField.tsx`
- `Select.tsx`, `SelectField.tsx`
- `Slider.tsx`, `SliderField.tsx`
- `Switch.tsx`, `SwitchField.tsx`
- `Textarea.tsx`, `TextAreaField.tsx`, `TextField.tsx`
- `TimeField.tsx`, `TimeZonePicker.tsx`
- `Toggle.tsx`, `ToggleGroup.tsx`

### Layout
- `AppLayout.tsx`
- `AspectRatio.tsx`
- `Card.tsx`
- `Collapsible.tsx`
- `InlineFieldGroup.tsx`
- `Item.tsx`
- `Resizable.tsx`
- `ScrollArea.tsx`
- `Separator.tsx`
- `Sheet.tsx`
- `Sidebar.tsx`
- `SidePane.tsx`

### Overlay
- `AlertDialog.tsx`, `UnsavedChangesAlertDialog.tsx`
- `Dialog.tsx`, `DirtyDialog.tsx`
- `Drawer.tsx`
- `HoverCard.tsx`
- `Popover.tsx`
- `Tooltip.tsx`

### Navigation
- `Breadcrumb.tsx`
- `ContextMenu.tsx`
- `DropdownMenu.tsx`
- `Link.tsx`
- `NavigationMenu.tsx`
- `Pagination.tsx`
- `Tabs.tsx`

### Feedback
- `Alert.tsx`
- `BannerPortal.tsx`
- `Empty.tsx`
- `Kbd.tsx`
- `Progress.tsx`
- `Skeleton.tsx`
- `Sonner.tsx`
- `Spinner.tsx`

### Data Display
- `Accordion.tsx`
- `Avatar.tsx`
- `Badge.tsx`
- `Calendar.tsx`
- `Chart.tsx`
- `Command.tsx`
- `Field.tsx`, `Form.tsx`, `Label.tsx`, `LabelWithTooltip.tsx`
- `LinkCard.tsx`
- `MarkdownRenderer.tsx`
- `Table.tsx`, `TablePagination.tsx`
- `TenantLogo.tsx`

### Misc / Context
- `AddToHomescreen.tsx` (PWA install)
- `DirtyDialogContext.ts` (context only)

**Total: 82 component files across 6 categories.**

---

## 2. Cal.com Primitives Mapping Table

Source root: `cal.com/packages/ui/components/`

| Cal.com component | Cal.com path | Nerova equivalent | Decision | Notes |
|---|---|---|---|---|
| `AddressInput` | `address/AddressInput.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/AddressInput.tsx` |
| `MultiEmail` | `address/MultiEmail.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/MultiEmail.tsx` |
| `Alert` | `alert/Alert.tsx` | `Alert.tsx` | **USE NEROVA** | Identical semantics |
| `AppListCard` | `app-list-card/AppListCard.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/AppListCard.tsx`; needs connector-aware props |
| `ArrowButton` | `arrow-button/ArrowButton.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/ArrowButton.tsx`; thin Button variant |
| `Avatar` | `avatar/Avatar.tsx` | `Avatar.tsx` | **USE NEROVA** | — |
| `AvatarGroup` | `avatar/AvatarGroup.tsx` | `Avatar.tsx` | **USE NEROVA** | Nerova Avatar supports group variant; verify max-display prop |
| `UserAvatar` | `avatar/UserAvatar.tsx` | `Avatar.tsx` | **USE NEROVA** | Wrap Nerova Avatar |
| `UserAvatarGroup` | `avatar/UserAvatarGroup.tsx` | `Avatar.tsx` | **USE NEROVA** | Wrap Nerova Avatar |
| `UserAvatarGroupWithOrg` | `avatar/UserAvatarGroupWithOrg.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/UserAvatarGroupWithOrg.tsx`; org-badge overlay |
| `Badge` | `badge/Badge.tsx` | `Badge.tsx` | **USE NEROVA** | — |
| `CreditsBadge` | `badge/CreditsBadge.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/CreditsBadge.tsx`; EE billing |
| `InfoBadge` | `badge/InfoBadge.tsx` | `Badge.tsx` + `Tooltip.tsx` | **USE NEROVA** | Compose from existing primitives |
| `UpgradeTeamsBadge` | `badge/UpgradeTeamsBadge.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/UpgradeTeamsBadge.tsx`; EE upsell |
| `Breadcrumb` | `breadcrumb/Breadcrumb.tsx` | `Breadcrumb.tsx` | **USE NEROVA** | — |
| `Button` | `button/Button.tsx` | `Button.tsx` | **USE NEROVA** | — |
| `LinkIconButton` | `button/LinkIconButton.tsx` | `Button.tsx` | **USE NEROVA** | Use `variant="ghost"` + icon slot |
| `SplitButton` | `button/SplitButton.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/SplitButton.tsx` |
| `ButtonGroup` | `buttonGroup/ButtonGroup.tsx` | `ButtonGroup.tsx` | **USE NEROVA** | — |
| `CalendarSwitch` | `calendar-switch/CalendarSwitch.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/CalendarSwitch.tsx`; toggle for calendar visibility |
| `Card` | `card/Card.tsx` | `Card.tsx` | **USE NEROVA** | — |
| `FormCard` | `card/FormCard.tsx` | `Card.tsx` | **USE NEROVA** | Compose with Card; no separate port needed |
| `PanelCard` | `card/PanelCard.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/PanelCard.tsx`; collapsible side-panel card |
| `StepCard` | `card/StepCard.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/StepCard.tsx`; onboarding wizard steps |
| `Checkbox` | `checkbox/Checkbox.tsx` | `Checkbox.tsx` | **USE NEROVA** | — |
| `MultiSelectCheckboxes` | `checkbox/MultiSelectCheckboxes.tsx` | `MultiSelect.tsx` | **USE NEROVA** | Map to Nerova MultiSelect |
| `ColorPicker` | `color-picker/colorpicker.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/ColorPicker.tsx`; team branding |
| `Command` | `command/Command.tsx` | `Command.tsx` | **USE NEROVA** | — |
| `Credits` | `credits/Credits.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/Credits.tsx`; EE billing display |
| `DateRangePicker` | `date-range-picker/DateRangePicker.tsx` | `DateRangePicker.tsx` | **USE NEROVA** | — |
| `DatePicker` | `datepicker/DatePicker.tsx` | `DatePicker.tsx` | **USE NEROVA** | — |
| `Dialog` | `dialog/Dialog.tsx` | `Dialog.tsx` | **USE NEROVA** | — |
| `ConfirmationDialogContent` | `dialog/ConfirmationDialogContent.tsx` | `AlertDialog.tsx` | **USE NEROVA** | Map to Nerova AlertDialog |
| `DisconnectIntegration` | `disconnect-calendar-integration/DisconnectIntegration.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/DisconnectIntegration.tsx`; calendar app connector |
| `Divider` | `divider/Divider.tsx` | `Separator.tsx` | **USE NEROVA** | Use Nerova Separator |
| `Dropdown` | `dropdown/Dropdown.tsx` | `DropdownMenu.tsx` | **USE NEROVA** | — |
| `EditableHeading` | `editable-heading/EditableHeading.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/EditableHeading.tsx`; inline rename for team name |
| `Editor` (Lexical) | `editor/Editor.tsx` | `MarkdownRenderer.tsx` (read-only only) | **PORT TO SHARED** | `application/shared-webapp/ui/components/Editor.tsx`; full Lexical editor; includes nodes + plugins sub-dirs |
| `EmptyScreen` | `empty-screen/EmptyScreen.tsx` | `Empty.tsx` | **USE NEROVA** | — |
| `ErrorBoundary` | `errorBoundary/ErrorBoundary.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/ErrorBoundary.tsx` |
| `FileUploader` | `file-uploader/FileUploader.tsx` | `Dropzone.tsx` | **USE NEROVA** | Nerova Dropzone covers semantics; verify file-type constraints |
| `FilterSelect` | `filter-select/index.tsx` | `Select.tsx` + `MultiSelect.tsx` | **USE NEROVA** | Compose from existing |
| `HoverCard` | `hover-card/index.tsx` | `HoverCard.tsx` | **USE NEROVA** | — |
| `Icon` | `icon/Icon.tsx` + `IconSprites.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/Icon.tsx`; Nerova uses Lucide directly; assess whether the sprite approach is needed |
| `Spinner` | `icon/Spinner.tsx` | `Spinner.tsx` | **USE NEROVA** | — |
| `BannerUploader` | `image-uploader/BannerUploader.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/BannerUploader.tsx`; org branding |
| `ImageUploader` | `image-uploader/ImageUploader.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/ImageUploader.tsx`; team logo / avatar crop |
| `Form` (inputs) | `inputs/Form.tsx` | `Form.tsx` | **USE NEROVA** | — |
| `Input` | `inputs/Input.tsx` | `Input.tsx` | **USE NEROVA** | — |
| `Label` | `inputs/Label.tsx` | `Label.tsx` | **USE NEROVA** | — |
| `TextField` | `inputs/TextField.tsx` | `TextField.tsx` | **USE NEROVA** | — |
| `MultiOptionInput` | `inputs/MultiOptionInput.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/MultiOptionInput.tsx`; custom option builder for routing forms |
| `ShellSubHeading` | `layout/ShellSubHeading.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/ShellSubHeading.tsx`; page-section heading |
| `WizardLayout` | `layout/WizardLayout.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/WizardLayout.tsx`; onboarding wrapper |
| `List` | `list/List.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/List.tsx`; generic item list with actions |
| `Logo` | `logo/Logo.tsx` | `TenantLogo.tsx` | **USE NEROVA** | Map to TenantLogo |
| `OrgBanner` | `organization-banner/OrgBanner.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/OrgBanner.tsx`; org impersonation banner |
| `Pagination` | `pagination/Pagination.tsx` | `Pagination.tsx` | **USE NEROVA** | — |
| `AnimatedPopover` | `popover/AnimatedPopover.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/AnimatedPopover.tsx`; filter chip popovers |
| `MeetingTimeInTimezones` | `popover/MeetingTimeInTimezones.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/MeetingTimeInTimezones.tsx`; multi-TZ time display |
| `Popover` | `popover/Popover.tsx` | `Popover.tsx` | **USE NEROVA** | — |
| `ProgressBar` | `progress-bar/ProgressBar.tsx` | `Progress.tsx` | **USE NEROVA** | — |
| `Radio` | `radio/Radio.tsx` | `RadioGroup.tsx` | **USE NEROVA** | — |
| `RadioAreaGroup` | `radio/RadioAreaGroup.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/RadioAreaGroup.tsx`; card-style radio selection |
| `ScrollableArea` | `scrollable/ScrollableArea.tsx` | `ScrollArea.tsx` | **USE NEROVA** | — |
| `Section` | `section/section.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/Section.tsx`; settings page section wrapper |
| `SegmentedControl` | `segmented-control/SegmentedControl.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/SegmentedControl.tsx`; view-mode toggle |
| `Select` | `select/Select.tsx` | `Select.tsx` | **USE NEROVA** | — |
| `Sheet` | `sheet/Sheet.tsx` | `Sheet.tsx` | **USE NEROVA** | — |
| `Loader` | `skeleton/Loader.tsx` | `Skeleton.tsx` | **USE NEROVA** | Use Skeleton as loader |
| `Skeleton` | `skeleton/Skeleton.tsx` | `Skeleton.tsx` | **USE NEROVA** | — |
| `Steps` | `step/Steps.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/Steps.tsx`; onboarding progress steps |
| `SettingsToggle` | `switch/SettingsToggle.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/SettingsToggle.tsx`; feature-flag toggle row with label+description |
| `Switch` | `switch/Switch.tsx` | `Switch.tsx` | **USE NEROVA** | — |
| `Table` | `table/Table.tsx` | `Table.tsx` | **USE NEROVA** | — |
| `TableNew` | `table/TableNew.tsx` | `Table.tsx` | **USE NEROVA** | Verify column-def API parity |
| `TableActions` | `table/TableActions.tsx` | `Table.tsx` | **USE NEROVA** | Compose from Table + DropdownMenu |
| `HorizontalTabs` | `tabs/HorizontalTabs.tsx` | `Tabs.tsx` | **USE NEROVA** | — |
| `VerticalTabs` | `tabs/VerticalTabs.tsx` | `Tabs.tsx` | **USE NEROVA** | Confirm vertical orientation prop |
| `showToast` / `ProgressToast` | `toast/` | `Sonner.tsx` | **USE NEROVA** | Replace showToast call sites with Nerova toast API |
| `BooleanToggleGroup` | `toggleGroup/BooleanToggleGroup.tsx` | `ToggleGroup.tsx` | **USE NEROVA** | — |
| `ToggleGroup` | `toggleGroup/ToggleGroup.tsx` | `ToggleGroup.tsx` | **USE NEROVA** | — |
| `TokenHandler` | `TokenHandler/TokenHandler.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/TokenHandler.tsx`; OAuth token display |
| `Tooltip` | `tooltip/Tooltip.tsx` | `Tooltip.tsx` | **USE NEROVA** | — |
| `TopBanner` | `top-banner/TopBanner.tsx` | `BannerPortal.tsx` | **PORT TO SHARED** | `application/shared-webapp/ui/components/TopBanner.tsx`; BannerPortal is a portal only; TopBanner has dismiss + variant semantics |
| `UnpublishedEntity` | `unpublished-entity/UnpublishedEntity.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/UnpublishedEntity.tsx`; draft/disabled event-type overlay |
| `WizardForm` | `wizard/WizardForm.tsx` | none | **PORT TO SHARED** | `application/shared-webapp/ui/components/WizardForm.tsx`; multi-step form shell |

**Summary: 45 USE NEROVA · 34 PORT TO SHARED**

---

## 3. Cal.com Hooks Mapping Table

Source root: `cal.com/packages/lib/hooks/`
Nerova root: `application/shared-webapp/ui/hooks/`

| Cal.com hook | Cal.com path | Nerova equivalent | Decision | Notes |
|---|---|---|---|---|
| `useAsPath` | `useAsPath.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useAsPath.ts`; Next.js path helper |
| `useCallbackRef` | `useCallbackRef.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useCallbackRef.ts`; stable callback ref utility |
| `useClientOnly` | `useClientOnly.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useClientOnly.ts`; SSR guard |
| `useCompatSearchParams` | `useCompatSearchParams.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useCompatSearchParams.ts`; pages + app router compat |
| `useCopy` | `useCopy.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useCopy.ts`; clipboard API |
| `useDebounce` | `useDebounce.ts` | `useDebounce.ts` | **USE NEROVA** | — |
| `useElementByClassName` | `useElementByClassName.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useElementByClassName.ts`; DOM selector |
| `useFillRemainingHeight` | `useFillRemainingHeight.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useFillRemainingHeight.ts`; layout utility |
| `useInViewObserver` | `useInViewObserver.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useInViewObserver.ts`; intersection observer |
| `useIsomorphicLayoutEffect` | `useIsomorphicLayoutEffect.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useIsomorphicLayoutEffect.ts`; SSR-safe layout effect |
| `useIsStandalone` | `useIsStandalone.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useIsStandalone.ts`; PWA standalone mode |
| `useKeyPress` | `useKeyPress.ts` | `useKeyboardNavigation.ts` | **PORT TO SHARED** | Cal.com API differs (generic key detection vs. list nav); port as `useKeyPress.ts` |
| `useLocale` | `useLocale.ts` | `translationContext.ts` | **PORT TO SHARED** | Different API; `translationContext` is React context; `useLocale` returns string + setter; port as thin adapter |
| `useMediaQuery` | `useMediaQuery.ts` | `useViewportResize.ts` (partial) | **PORT TO SHARED** | `useViewportResize` returns dimensions; cal.com hook accepts arbitrary CSS MQ; port `useMediaQuery.ts` |
| `useOnclickOutside` | `useOnclickOutside.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useOnclickOutside.ts` |
| `usePagination` | `usePagination.ts` | none (component only) | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/usePagination.ts`; headless pagination logic |
| `useParamsWithFallback` | `useParamsWithFallback.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useParamsWithFallback.ts`; RSC param compat |
| `useRefreshData` | `useRefreshData.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useRefreshData.ts`; router.refresh wrapper |
| `useResponsive` | `useResponsive.ts` | `useViewportResize.ts` | **USE NEROVA** | Map breakpoint queries to Nerova `useViewportResize`; thin adapter needed |
| `useRouterQuery` | `useRouterQuery.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useRouterQuery.ts`; typed URL query |
| `useTheme` | `useTheme.ts` | `ThemeMode.tsx` (component) | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useTheme.ts`; headless theme toggle hook |
| `useTraceUpdate` | `useTraceUpdate.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useTraceUpdate.ts`; dev-only re-render tracer |
| `useTypedQuery` | `useTypedQuery.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useTypedQuery.ts`; Zod-validated search params |
| `useUrlMatchesCurrentUrl` | `useUrlMatchesCurrentUrl.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useUrlMatchesCurrentUrl.ts`; active-link helper |
| `useUserAgentData` | `useUserAgentData.ts` | none | **PORT TO SHARED** | `application/shared-webapp/ui/hooks/useUserAgentData.ts`; user-agent client hints |

**Summary: 2 USE NEROVA · 23 PORT TO SHARED**

---

## 4. Feature-Specific Composites

> **Note:** `cal.com/packages/features/ee` holds mostly server-side services. The UI composite components for Teams/Orgs/Workflows live predominantly in `cal.com/apps/web/` (Next.js pages and feature folders). The rows below capture what was found in `packages/features/ee` plus the most critical composites identified in `apps/web` by feature area.

### 4a. Teams

| Cal.com component | Cal.com path | Target Nerova path | Notes |
|---|---|---|---|
| `TeamEventTypeForm` | `packages/features/ee/teams/components/TeamEventTypeForm.tsx` | `application/main/WebApp/scheduling/teams/components/TeamEventTypeForm.tsx` | Full event-type form for team context |
| `MemberInvitationModal` | `apps/web/components/team/MemberInvitationModal.tsx` | `application/main/WebApp/scheduling/teams/components/MemberInvitationModal.tsx` | Invite by email or existing user |
| `MemberList` | `apps/web/components/team/MemberList.tsx` | `application/main/WebApp/scheduling/teams/components/MemberList.tsx` | Paginated member table with role badges |
| `TeamBranding` | `apps/web/components/team/TeamBranding.tsx` | `application/main/WebApp/scheduling/teams/components/TeamBranding.tsx` | Logo + color picker; depends on `ImageUploader`, `ColorPicker` |
| `RoleSelector` | `apps/web/components/team/RoleSelector.tsx` | `application/main/WebApp/scheduling/teams/components/RoleSelector.tsx` | Dropdown of OWNER/ADMIN/MEMBER roles |
| `TeamGeneralSettings` | `apps/web/pages/settings/teams/[id]/general.tsx` | `application/main/WebApp/scheduling/teams/pages/TeamGeneralSettingsPage.tsx` | Team name, slug, bio, branding |
| `PendingInvites` | `apps/web/components/team/PendingInvites.tsx` | `application/main/WebApp/scheduling/teams/components/PendingInvites.tsx` | Accept/decline invite list |

### 4b. Organizations

| Cal.com component | Cal.com path | Target Nerova path | Notes |
|---|---|---|---|
| `OrgGeneralSettings` | `apps/web/pages/settings/organizations/[id]/general.tsx` | `application/main/WebApp/scheduling/organizations/pages/OrgGeneralSettingsPage.tsx` | Org name, slug, banner; depends on `OrgBanner`, `BannerUploader` |
| `OrgMembersPage` | `apps/web/pages/settings/organizations/[id]/members.tsx` | `application/main/WebApp/scheduling/organizations/pages/OrgMembersPage.tsx` | Multi-team member management |
| `OrganizationTeamsList` | `apps/web/components/org/OrganizationTeamsList.tsx` | `application/main/WebApp/scheduling/organizations/components/OrganizationTeamsList.tsx` | Teams within org; create/delete |
| `SubdomainSetup` | `apps/web/components/org/SubdomainSetup.tsx` | `application/main/WebApp/scheduling/organizations/components/SubdomainSetup.tsx` | Org subdomain configuration |

### 4c. Workflows

| Cal.com component | Cal.com path | Target Nerova path | Notes |
|---|---|---|---|
| `WorkflowListItem` | `apps/web/components/workflows/WorkflowListItem.tsx` | `application/main/WebApp/scheduling/workflows/components/WorkflowListItem.tsx` | List row with name, trigger, actions |
| `WorkflowStepContainer` | `apps/web/components/workflows/WorkflowStepContainer.tsx` | `application/main/WebApp/scheduling/workflows/components/WorkflowStepContainer.tsx` | Drag-and-drop step builder |
| `getActionIcon` | `packages/features/ee/workflows/getActionIcon.tsx` | `application/main/WebApp/scheduling/workflows/components/getActionIcon.tsx` | Icon helper per action type; direct port |
| `WorkflowPermissionGuard` | `packages/features/pbac/client/WorkflowTabPermissionGuard.tsx` | `application/main/WebApp/scheduling/workflows/components/WorkflowPermissionGuard.tsx` | PBAC guard; wraps pbac `usePermissionStore` |

### 4d. SSO / dsync

| Cal.com component | Cal.com path | Target Nerova path | Notes |
|---|---|---|---|
| `SSOConfiguration` | `apps/web/pages/settings/security/sso.tsx` | `application/main/WebApp/scheduling/settings/security/SSOConfigurationPage.tsx` | SAML/OIDC setup form |
| `DirectorySyncSetup` | `apps/web/pages/settings/organizations/[id]/dsync.tsx` | `application/main/WebApp/scheduling/organizations/pages/DirectorySyncPage.tsx` | BoxyHQ dsync config |

### 4e. Managed Event Types

| Cal.com component | Cal.com path | Target Nerova path | Notes |
|---|---|---|---|
| `ManagedEventTypesList` | `apps/web/pages/settings/organizations/[id]/event-types.tsx` | `application/main/WebApp/scheduling/organizations/pages/ManagedEventTypesPage.tsx` | Org-level managed event types |
| `LockedFieldsManager` (hook) | `packages/features/ee/managed-event-types/useLockedFieldsManager.tsx` | `application/main/WebApp/scheduling/event-types/hooks/useLockedFieldsManager.ts` | Hook; direct port |

### 4f. Insights / Booking Audit

| Cal.com component | Cal.com path | Target Nerova path | Notes |
|---|---|---|---|
| _(no tsx components in packages)_ | `packages/features/insights/` (server-side only) | — | All UI lives in `apps/web`; identify page components in Wave 4 planning |
| _(no tsx components in packages)_ | `packages/features/booking-audit/` (server-side only) | — | Same as insights |

### 4g. Attributes / PBAC

| Cal.com component | Cal.com path | Target Nerova path | Notes |
|---|---|---|---|
| `EventPermissionContext` | `packages/features/pbac/client/context/EventPermissionContext.tsx` | `application/main/WebApp/scheduling/pbac/EventPermissionContext.tsx` | Direct port; Zustand context |
| `WorkflowTabPermissionGuard` | `packages/features/pbac/client/WorkflowTabPermissionGuard.tsx` | `application/main/WebApp/scheduling/pbac/WorkflowTabPermissionGuard.tsx` | Direct port |

---

## 5. State Management

Zustand stores found in `cal.com/packages/features/**`. EE-relevant stores only.

| Store | Cal.com path | Description | Target Nerova path | Placement rationale |
|---|---|---|---|---|
| `BookerStore` | `features/bookings/Booker/store.ts` | Booking widget state: slot selection, date, layout mode, booking payload, guest list, ISO country codes | `application/main/WebApp/scheduling/booker/store.ts` | Feature-local; consumed only by Booker widget SCS |
| `timePreferencesStore` / `useTimePreferences` | `features/bookings/lib/timePreferences.ts` | Time format (12h/24h) + timezone; stored in localStorage | `application/shared-webapp/state/timePreferences.ts` | **Port to shared-webapp/state** — consumed by booker, availability, and event-type forms across SCSs |
| `CalendarStore` / `createCalendarStore` | `features/calendars/weeklyview/state/store.ts` | Weekly calendar view state: dates, events, timezone, grid config | `application/main/WebApp/scheduling/calendars/weeklyview/state/store.ts` | Feature-local; context-provider pattern; port as-is |
| `usePermissionStore` | `features/pbac/infrastructure/store/permission-store.ts` | RBAC: `Map<teamId, { roleId, permissions: Set<PermissionString> }>` | `application/main/WebApp/scheduling/pbac/store/permission-store.ts` | Feature-local to scheduling SCS; initialized from API response at layout boundary |
| `useEventPermissionStore` (via `EventPermissionContext`) | `features/pbac/client/context/EventPermissionContext.tsx` | Event-type + workflow CRUD permission flags per page context | `application/main/WebApp/scheduling/pbac/context/EventPermissionContext.tsx` | Feature-local; React context store; direct port |
| `TroubleshooterStore` | `features/troubleshooter/store.ts` | Availability debugger state: selected event, month, date, calendar-color map | `application/main/WebApp/scheduling/troubleshooter/store.ts` | Feature-local; defer to Wave 5 (troubleshooter is v2 scope) |

**No EE-specific Zustand stores were found directly inside `packages/features/ee`. All EE state flows through the six stores above or through React Query.**

---

## 6. Open Questions

1. **Icon strategy**: Cal.com ships an `Icon.tsx` + SVG sprite system (`IconSprites.tsx`). Nerova uses Lucide directly. Confirm whether the sprite approach is needed for performance, or whether Lucide tree-shaking is sufficient. Decision affects whether `Icon.tsx` port is needed at all.

2. **Editor (Lexical) scope**: The full Lexical editor (`editor/`, `nodes/`, `plugins/`) is a significant dependency bundle. Confirm whether rich-text workflow step messages and event-type descriptions require the full editor in Wave 4, or if a `<textarea>` + Markdown preview is acceptable for v1.

3. **`apps/web` composite components**: This inventory was bounded to `packages/features/ee`. The majority of EE composites (team settings pages, org settings pages, workflow builder UI) live in `cal.com/apps/web/`. A separate follow-up pass over `apps/web` is needed to produce complete per-page composite inventories for Sections 4a–4f before implementation work begins.

4. **`SettingsToggle` vs `SwitchField`**: Nerova has `SwitchField.tsx` which may cover `SettingsToggle` semantics (label + description + switch in one row). Verify before porting.

5. **`TopBanner` vs `BannerPortal`**: Nerova's `BannerPortal` is a portal mechanism without built-in dismiss/variant. Confirm whether the TopBanner port should extend BannerPortal or replace it with a standalone component.

6. **`useLocale` adapter**: Cal.com's `useLocale` is imported widely in EE components. Determine the exact mapping to Nerova's `translationContext` / i18n infrastructure before beginning composite ports to avoid per-file rework.

7. **Zustand version alignment**: Confirm that Nerova's React version and Zustand version are compatible with the `createWithEqualityFn` and `createStore` API patterns used in the cal.com stores (Zustand v4+).

8. **`TroubleshooterStore` deferral**: Marked as Wave 5 scope — confirm with product that availability troubleshooter is not a Wave 4 deliverable.

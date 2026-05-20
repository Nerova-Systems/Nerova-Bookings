# Cal.com UI Parity Wave 1.14

## Scope

Event Types list and Event Type Editor parity correction against imported Cal.com baseline `cf2a55c42363ab79982eef11610e1de8151b45ce`.

## Copy-First Evidence

Exact Cal.com source rows were copied before runtime adaptation into:

`application/main/WebApp/routes/-scheduling/calcom-port-source/ui-parity/wave-1-14/`

| Source path | Copied checksum | Runtime target |
| --- | --- | --- |
| `apps/web/modules/event-types/views/event-types-listing-view.tsx` | `023c870423e76fd2dfa80b34842b830a8a81519ddfab6686925b6d28d6de048a` | `application/main/WebApp/routes/-scheduling/event-types-shell/EventTypesList.tsx` |
| `apps/web/modules/event-types/components/EventTypeLayout.tsx` | `2ba4c6fe43b0bdb14587af17185fd77bd94cf6894ce4712caf2201b324f2ce82` | `application/main/WebApp/routes/-scheduling/event-types-shell/*` |
| `apps/web/modules/event-types/components/EventTypeWebWrapper.tsx` | `5560ce80748ceb61a65ab973390be9a5eef9695e5b276b64f0d9ed0b3e31a66d` | `application/main/WebApp/routes/event-types/$eventTypeId.tsx` |
| `apps/web/modules/event-types/components/tabs/setup/EventSetupTab.tsx` | `e93f431a42b166fee6403482009e8489234ddec00f6f18c88917dc1eb4e8f332` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeSetupTab.tsx` |
| `apps/web/modules/event-types/components/tabs/availability/EventAvailabilityTab.tsx` | `f6ead316b5aa5f176b03bba0b6383328ef7b858cd05ccedfb6bf9f42bf18de04` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAvailabilityTab.tsx` |
| `apps/web/modules/event-types/components/tabs/limits/EventLimitsTabWebWrapper.tsx` | `3a3043a54a0c1b87e754adbd45320f3b8e48baee0c009f7a8c99ac4c6f681fdd` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeLimitsTab.tsx` |
| `apps/web/modules/event-types/components/tabs/advanced/EventAdvancedTab.tsx` | `f6b2d01e292ae3579579b0aee80dab8a92ef192904913eab17a869ef7f20a939` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAdvancedTab.tsx` |
| `apps/web/modules/event-types/components/tabs/advanced/RequiresConfirmationController.tsx` | `551cc9790f50f7e8183e699d0daa963ac756b2d9a55ad6f87acff853674e6182` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAdvancedTab.tsx` |
| `apps/web/modules/event-types/components/tabs/advanced/DisableReschedulingController.tsx` | `a66942d8362be873b9ab50fbb15e5905dfa63818c56f7ee80604a32599aed580` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeAdvancedTab.tsx` |
| `apps/web/modules/event-types/components/tabs/recurring/EventRecurringWebWrapper.tsx` | `d0cc2b3f065b3d84f9f45ed0e19d5ddd2309ec9a22844c78cf55a779f4e52670` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeRecurringTab.tsx` |
| `apps/web/modules/event-types/components/tabs/workflows/EventWorkflowsTab.tsx` | `53e0fb254fab69f015c29d7036e54d1324203b83116c20e4e6591a63b1ebd2c2` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeWorkflowsTab.tsx` |
| `apps/web/modules/event-types/components/tabs/webhooks/EventWebhooksTab.tsx` | `e6a79a0dda38d8ed00079f83268c258e87b6c862981f8adf0d97b449768a63fc` | `application/main/WebApp/routes/-scheduling/event-type-tabs/EventTypeWebhooksTab.tsx` |
| `packages/ui/components/navigation/tabs/VerticalTabs.tsx` | `dde241454d2b42ba0d29520d9ebca0d5d373dc77dc6bdacba6bc81e06a4bc9bd` | `application/shared-webapp/ui/components/VerticalTabs.tsx` |
| `packages/ui/components/navigation/tabs/VerticalTabItem.tsx` | `35c79be19f6b867e8880f9364c17bdf5f054a23346c0008baad846bcb9dde4a3` | `application/shared-webapp/ui/components/VerticalTabs.tsx` |
| `packages/ui/components/navigation/tabs/HorizontalTabs.tsx` | `0b7c0d48acfcf25c027172afc81ef014bb566b0e85877fc30e90ea382d01067d` | `application/shared-webapp/ui/components/HorizontalTabs.tsx` |
| `packages/ui/components/form/switch/SettingsToggle.tsx` | `af0d2878bfdf4b0f1fbf728091fb690c297d3fd4608e3a7cc1141d5883332ac1` | `application/shared-webapp/ui/components/SettingsToggle.tsx` |

## Runtime Changes

- Event Types list now uses a Cal.com-shaped title/search/list-row/action-group composition.
- Event Type Editor exposes Cal.com tab labels/order: Basics, Availability, Limits, Advanced, Recurring, Apps, Workflows, Webhooks.
- Shared `VerticalTabs`, `HorizontalTabs`, and `SettingsToggle` primitives were added because existing primitives blocked the Cal.com tab/card structure.
- Advanced tab now starts with Cal.com-style settings cards for calendar event naming, destination calendar, layout/default view, booking questions, cancellation reason, confirmation, cancellation/reschedule toggles, and Cal Video transcription state.

## Verification

- Red parity proof: `dotnet run --project developer-cli -- e2e "cal-com-ui-parity" --quiet --stop-on-first-failure` failed before the fix because the editor only rendered `Setup Availability Limits Advanced Workflows Webhooks Recurring Dependencies`.
- Current build proof: `dotnet run --project developer-cli -- build --frontend --quiet` passes after the Wave 1.14 runtime changes.

## Remaining UI Gaps

- This wave corrects Event Types list/editor layout and content drift; it does not complete pixel-perfect parity for the global Cal.com shell/sidebar.
- Embed behavior, full app-store breadth, and advanced Cal.com settings that require unavailable backend behavior remain ledgered for later slices.

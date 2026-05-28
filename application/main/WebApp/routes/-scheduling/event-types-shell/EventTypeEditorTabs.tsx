import { t } from "@lingui/core/macro";
import { Form } from "@repo/ui/components/Form";
import { HorizontalTabs } from "@repo/ui/components/HorizontalTabs";
import { VerticalTabs, type VerticalTabItem } from "@repo/ui/components/VerticalTabs";
import { useNavigate } from "@tanstack/react-router";
import {
  CalendarIcon,
  ClockIcon,
  Grid3X3Icon,
  LinkIcon,
  RefreshCcwIcon,
  SlidersHorizontalIcon,
  WebhookIcon,
  ZapIcon
} from "lucide-react";

import type { ApiValidationError, EventTypePayload, Schedule } from "../schedulingTypes";

import { EventTypeAdvancedTab } from "../event-type-tabs/EventTypeAdvancedTab";
import { EventTypeAiVoiceAgentTab } from "../event-type-tabs/EventTypeAiVoiceAgentTab";
import { EventTypeAppsTab } from "../event-type-tabs/EventTypeAppsTab";
import { EventTypeAvailabilityTab } from "../event-type-tabs/EventTypeAvailabilityTab";
import { EventTypeInstantMeetingTab } from "../event-type-tabs/EventTypeInstantMeetingTab";
import { EventTypeLimitsTab } from "../event-type-tabs/EventTypeLimitsTab";
import { EventTypeRecurringTab } from "../event-type-tabs/EventTypeRecurringTab";
import { EventTypeSetupTab } from "../event-type-tabs/EventTypeSetupTab";
import { EventTypeTeamTab } from "../event-type-tabs/EventTypeTeamTab";
import { type EventTypeTabProps } from "../event-type-tabs/EventTypeTabTypes";
import { EventTypeWebhooksTab } from "../event-type-tabs/EventTypeWebhooksTab";
import { EventTypeWorkflowsTab } from "../event-type-tabs/EventTypeWorkflowsTab";
import { eventTypeTabNames, getEventTypeTabLabel, type EventTypeTabName } from "./eventTypeShellTypes";

export const eventTypeFormId = "event-type-editor-form";

type EventTypeEditorTabsProps = Readonly<{
  eventTypeId: string;
  tabName: EventTypeTabName;
  draft: EventTypePayload;
  schedules: Schedule[];
  canSave: boolean;
  error?: ApiValidationError;
  onChange: (value: EventTypePayload) => void;
  onSubmit: () => void;
}>;

export function EventTypeEditorTabs({
  eventTypeId,
  tabName,
  draft,
  schedules,
  canSave,
  error,
  onChange,
  onSubmit
}: EventTypeEditorTabsProps) {
  const navigate = useNavigate();
  const tabProps = { eventTypeId, value: draft, schedules, onChange, error };
  const activeTabName = tabName === "dependencies" ? "apps" : tabName;
  const tabs = getEventTypeTabs(draft, schedules);
  const contentClassName = "min-w-0 rounded-lg border bg-background p-4 md:p-6";
  const navigateToTab = (nextTabName: string) =>
    navigate({
      to: "/event-types/$eventTypeId",
      params: { eventTypeId },
      search: { tabName: nextTabName as EventTypeTabName }
    });

  return (
    <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:gap-6">
      <VerticalTabs
        tabs={tabs}
        value={activeTabName}
        onValueChange={navigateToTab}
        className="hidden w-64 shrink-0 xl:sticky xl:top-20 xl:flex"
        itemClassName="items-start"
        data-testid="event-type-vertical-tabs"
      />
      <Form
        id={eventTypeFormId}
        validationBehavior="aria"
        validationErrors={error?.errors}
        className="min-w-0 flex-1"
        onSubmit={(event) => {
          event.preventDefault();
          if (canSave) onSubmit();
        }}
      >
        <HorizontalTabs
          tabs={tabs}
          value={activeTabName}
          onValueChange={navigateToTab}
          className="mb-4 xl:hidden"
          data-testid="event-type-horizontal-tabs"
        />
        <div className={contentClassName}>{renderEventTypeTab(activeTabName, tabProps, eventTypeId)}</div>
      </Form>
    </div>
  );
}

function getEventTypeTabs(draft: EventTypePayload, schedules: Schedule[]): VerticalTabItem[] {
  const scheduleName = schedules.find((schedule) => schedule.id === draft.scheduleId)?.name ?? "Working hours";
  return eventTypeTabNames.map((tabName) => ({
    value: tabName,
    label: getEventTypeTabLabel(tabName),
    description: getEventTypeTabDescription(tabName, draft, scheduleName),
    icon: getEventTypeTabIcon(tabName)
  }));
}

function getEventTypeTabDescription(tabName: EventTypeTabName, draft: EventTypePayload, scheduleName: string) {
  switch (tabName) {
    case "setup":
      return `${draft.durationMinutes} mins`;
    case "availability":
      return scheduleName;
    case "limits":
      return t`How often you can be booked`;
    case "advanced":
      return t`Calendar settings & more...`;
    case "recurring":
      return t`Set up a repeating schedule`;
    case "apps":
      return t`0 apps, 0 active`;
    case "workflows":
      return t`0 active`;
    case "webhooks":
      return t`0 active`;
    case "dependencies":
      return "";
  }
}

function getEventTypeTabIcon(tabName: EventTypeTabName) {
  switch (tabName) {
    case "setup":
      return <LinkIcon />;
    case "availability":
      return <CalendarIcon />;
    case "limits":
      return <ClockIcon />;
    case "advanced":
      return <SlidersHorizontalIcon />;
    case "recurring":
      return <RefreshCcwIcon />;
    case "apps":
      return <Grid3X3Icon />;
    case "workflows":
      return <ZapIcon />;
    case "webhooks":
      return <WebhookIcon />;
    case "dependencies":
      return null;
  }
}

function renderEventTypeTab(
  tabName: Exclude<EventTypeTabName, "dependencies">,
  tabProps: EventTypeTabProps,
  eventTypeId: string
) {
  switch (tabName) {
    case "setup":
      return <EventTypeSetupTab {...tabProps} />;
    case "availability":
      return <EventTypeAvailabilityTab {...tabProps} />;
    case "limits":
      return <EventTypeLimitsTab {...tabProps} />;
    case "advanced":
      return <EventTypeAdvancedTab {...tabProps} />;
    case "recurring":
      return <EventTypeRecurringTab {...tabProps} />;
    case "apps":
      return <EventTypeAppsTab />;
    case "workflows":
      return <EventTypeWorkflowsTab eventTypeId={eventTypeId} />;
    case "webhooks":
      return <EventTypeWebhooksTab eventTypeId={eventTypeId} />;
  }
}

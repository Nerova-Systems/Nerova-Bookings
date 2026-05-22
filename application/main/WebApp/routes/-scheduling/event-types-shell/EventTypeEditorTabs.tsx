import { Form } from "@repo/ui/components/Form";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import { useNavigate } from "@tanstack/react-router";

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

  return (
    <Tabs
      value={tabName}
      className="grid gap-6 lg:grid-cols-[12rem_minmax(0,1fr)]"
      onValueChange={(nextTabName) =>
        navigate({
          to: "/event-types/$eventTypeId",
          params: { eventTypeId },
          search: { tabName: nextTabName as EventTypeTabName }
        })
      }
    >
      <TabsList className="border-b lg:flex-col lg:items-stretch lg:overflow-visible lg:border-r lg:border-b-0">
        {eventTypeTabNames.map((eventTypeTabName) => (
          <TabsTrigger key={eventTypeTabName} value={eventTypeTabName} className="lg:justify-start lg:after:hidden">
            {getEventTypeTabLabel(eventTypeTabName)}
          </TabsTrigger>
        ))}
      </TabsList>
      <Form
        id={eventTypeFormId}
        validationBehavior="aria"
        validationErrors={error?.errors}
        className="min-w-0"
        onSubmit={(event) => {
          event.preventDefault();
          if (canSave) onSubmit();
        }}
      >
        <TabsContent value="setup" className="min-w-0">
          <EventTypeSetupTab {...tabProps} />
        </TabsContent>
        <TabsContent value="availability" className="min-w-0">
          <EventTypeAvailabilityTab {...tabProps} />
        </TabsContent>
        <TabsContent value="limits" className="min-w-0">
          <EventTypeLimitsTab {...tabProps} />
        </TabsContent>
        <TabsContent value="advanced" className="min-w-0">
          <EventTypeAdvancedTab {...tabProps} />
        </TabsContent>
        <TabsContent value="recurring" className="min-w-0">
          <EventTypeRecurringTab {...tabProps} />
        </TabsContent>
        <TabsContent value="team" className="min-w-0">
          <EventTypeTeamTab {...tabProps} />
        </TabsContent>
        <TabsContent value="instant-meeting" className="min-w-0">
          <EventTypeInstantMeetingTab {...tabProps} />
        </TabsContent>
        <TabsContent value="ai-voice-agent" className="min-w-0">
          <EventTypeAiVoiceAgentTab {...tabProps} />
        </TabsContent>
        <TabsContent value="workflows" className="min-w-0">
          <EventTypeWorkflowsTab {...tabProps} />
        </TabsContent>
        <TabsContent value="webhooks" className="min-w-0">
          <EventTypeWebhooksTab {...tabProps} />
        </TabsContent>
        <TabsContent value="apps" className="min-w-0">
          <EventTypeAppsTab {...tabProps} />
        </TabsContent>
      </Form>
    </Tabs>
  );
}

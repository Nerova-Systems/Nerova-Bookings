import { Form } from "@repo/ui/components/Form";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import { useNavigate } from "@tanstack/react-router";

import type { ApiValidationError, EventTypePayload, Schedule } from "../schedulingTypes";

import { EventTypeAdvancedTab } from "../event-type-tabs/EventTypeAdvancedTab";
import { EventTypeAvailabilityTab } from "../event-type-tabs/EventTypeAvailabilityTab";
import { EventTypeDependenciesTab } from "../event-type-tabs/EventTypeDependenciesTab";
import { EventTypeLimitsTab } from "../event-type-tabs/EventTypeLimitsTab";
import { EventTypeRecurringTab } from "../event-type-tabs/EventTypeRecurringTab";
import { EventTypeSetupTab } from "../event-type-tabs/EventTypeSetupTab";
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
  const tabProps = { value: draft, schedules, onChange, error };

  return (
    <Tabs
      value={tabName}
      className="flex flex-col gap-4 xl:grid xl:grid-cols-[16rem_minmax(0,1fr)] xl:gap-6"
      onValueChange={(nextTabName) =>
        navigate({
          to: "/event-types/$eventTypeId",
          params: { eventTypeId },
          search: { tabName: nextTabName as EventTypeTabName }
        })
      }
    >
      <TabsList
        className="border-b xl:sticky xl:top-20 xl:h-fit xl:flex-col xl:items-stretch xl:overflow-visible xl:border-r xl:border-b-0"
        data-testid="event-type-vertical-tabs"
      >
        {eventTypeTabNames.map((eventTypeTabName) => (
          <TabsTrigger key={eventTypeTabName} value={eventTypeTabName} className="xl:justify-start xl:after:hidden">
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
        <TabsContent value="setup" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeSetupTab {...tabProps} />
        </TabsContent>
        <TabsContent value="availability" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeAvailabilityTab {...tabProps} />
        </TabsContent>
        <TabsContent value="limits" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeLimitsTab {...tabProps} />
        </TabsContent>
        <TabsContent value="advanced" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeAdvancedTab {...tabProps} />
        </TabsContent>
        <TabsContent value="workflows" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeWorkflowsTab eventTypeId={eventTypeId} />
        </TabsContent>
        <TabsContent value="webhooks" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeWebhooksTab eventTypeId={eventTypeId} />
        </TabsContent>
        <TabsContent value="recurring" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeRecurringTab {...tabProps} />
        </TabsContent>
        <TabsContent value="dependencies" className="min-w-0 rounded-md border bg-background p-4 md:p-6">
          <EventTypeDependenciesTab {...tabProps} />
        </TabsContent>
      </Form>
    </Tabs>
  );
}

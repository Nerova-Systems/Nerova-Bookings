import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Switch } from "@repo/ui/components/Switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@repo/ui/components/Tabs";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, SaveIcon, Trash2Icon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { EventTypeForm } from "../-scheduling/EventTypeForm";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { CopyEventTypeButton, PreviewEventTypeButton } from "../-scheduling/event-types-shell/EventTypeActionButtons";
import { DeleteEventTypeDialog } from "../-scheduling/event-types-shell/DeleteEventTypeDialog";
import { EventTypePlaceholderTab } from "../-scheduling/event-types-shell/EventTypePlaceholderTabs";
import {
  eventTypeTabs,
  getEventTypePublicUrl,
  isEventTypeTabName,
  type EventTypeTabName
} from "../-scheduling/event-types-shell/eventTypeShellTypes";
import { eventTypeToPayload, isEventTypePayloadSubmittable, type EventTypePayload } from "../-scheduling/schedulingTypes";

const eventTypeFormId = "event-type-editor-form";

export const Route = createFileRoute("/event-types/$eventTypeId")({
  staticData: { trackingTitle: "Event type details" },
  validateSearch: (search: Record<string, unknown>) => ({
    tabName: isEventTypeTabName(search.tabName) ? search.tabName : "setup"
  }),
  component: EventTypeDetailsPage
});

function EventTypeDetailsPage() {
  const { eventTypeId } = Route.useParams();
  const { tabName } = Route.useSearch();
  const navigate = useNavigate();
  const { data: eventType, isLoading } = api.useQuery("get", "/api/event-types/{id}", {
    params: { path: { id: eventTypeId } }
  });
  const { data: schedulesData } = api.useQuery("get", "/api/schedules");
  const schedules = schedulesData?.schedules ?? [];
  const [draft, setDraft] = useState<EventTypePayload | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  useEffect(() => {
    if (eventType) setDraft(eventTypeToPayload(eventType));
  }, [eventType]);

  const savedPayload = useMemo(() => (eventType ? eventTypeToPayload(eventType) : null), [eventType]);
  const isDirty = draft !== null && savedPayload !== null && JSON.stringify(draft) !== JSON.stringify(savedPayload);
  const canSave = draft !== null && schedules.length > 0 && isEventTypePayloadSubmittable(draft) && isDirty;

  const updateEventTypeMutation = api.useMutation("put", "/api/event-types/{id}", {
    onSuccess: (updatedEventType) => {
      toast.success(t`Event type updated`);
      setDraft(eventTypeToPayload(updatedEventType));
      void queryClient.invalidateQueries();
    }
  });

  const handleDraftChange = (nextDraft: EventTypePayload) => {
    setDraft(nextDraft);
  };

  return (
    <SchedulingPageShell
      title={eventType?.title ?? t`Event type`}
      subtitle={eventType ? getEventTypePublicUrl(eventType) : t`Edit booking setup for this appointment type.`}
      maxWidth="80rem"
      titleContent={
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() =>
                navigate({ to: "/event-types", search: { dialog: undefined, duplicateEventTypeId: undefined } })
              }
            >
              <ArrowLeftIcon />
              <Trans>Back</Trans>
            </Button>
            <span className="truncate">{eventType?.title ?? t`Event type`}</span>
          </div>
        </div>
      }
      actions={
        eventType && draft ? (
          <div className="flex items-center gap-2">
            <label className="hidden items-center gap-2 text-sm text-muted-foreground md:flex">
              <Switch
                checked={!draft.hidden}
                onCheckedChange={(checked) => handleDraftChange({ ...draft, hidden: !checked })}
              />
              <Trans>Show on profile</Trans>
            </label>
            <CopyEventTypeButton eventType={eventType} />
            <PreviewEventTypeButton eventType={eventType} />
            <Button type="button" variant="outline" size="sm" onClick={() => setDeleteDialogOpen(true)}>
              <Trash2Icon />
              <Trans>Delete</Trans>
            </Button>
            <Button
              type="submit"
              form={eventTypeFormId}
              size="sm"
              disabled={!canSave}
              isPending={updateEventTypeMutation.isPending}
            >
              <SaveIcon />
              {updateEventTypeMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save</Trans>}
            </Button>
          </div>
        ) : null
      }
    >
      <GeneralApiErrors error={updateEventTypeMutation.error} />
      {isLoading || !draft || !eventType ? (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>Loading event type...</Trans>
        </div>
      ) : (
        <>
          <div className="mb-4 flex items-center justify-between gap-3 rounded-md border p-3 md:hidden">
            <span className="text-sm font-medium">
              <Trans>Show on profile</Trans>
            </span>
            <Switch checked={!draft.hidden} onCheckedChange={(checked) => handleDraftChange({ ...draft, hidden: !checked })} />
          </div>
          <Tabs
            value={tabName}
            onValueChange={(nextTabName) =>
              navigate({
                to: "/event-types/$eventTypeId",
                params: { eventTypeId },
                search: { tabName: nextTabName as EventTypeTabName }
              })
            }
          >
            <TabsList>
              {eventTypeTabs.map((tab) => (
                <TabsTrigger key={tab.name} value={tab.name}>
                  {tab.label}
                </TabsTrigger>
              ))}
            </TabsList>
            <TabsContent value="setup">
              <EventTypeForm
                formId={eventTypeFormId}
                value={draft}
                schedules={schedules}
                onChange={handleDraftChange}
                onSubmit={(body) =>
                  updateEventTypeMutation.mutate({
                    params: { path: { id: eventTypeId } },
                    body: { ...body, id: eventTypeId }
                  })
                }
                error={updateEventTypeMutation.error}
                isPending={updateEventTypeMutation.isPending}
                submitLabel={t`Save event type`}
                showSubmit={false}
              />
            </TabsContent>
            <TabsContent value="availability">
              <EventTypePlaceholderTab name={t`Availability`} />
            </TabsContent>
            <TabsContent value="limits">
              <EventTypePlaceholderTab name={t`Limits`} />
            </TabsContent>
            <TabsContent value="advanced">
              <EventTypePlaceholderTab name={t`Advanced`} />
            </TabsContent>
          </Tabs>
        </>
      )}
      <DeleteEventTypeDialog
        eventType={eventType ?? null}
        isOpen={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        onDeleted={() =>
          navigate({ to: "/event-types", search: { dialog: undefined, duplicateEventTypeId: undefined } })
        }
      />
    </SchedulingPageShell>
  );
}

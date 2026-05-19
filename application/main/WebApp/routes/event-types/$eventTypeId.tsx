import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Switch } from "@repo/ui/components/Switch";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { DeleteEventTypeDialog } from "../-scheduling/event-types-shell/DeleteEventTypeDialog";
import { EventTypeEditorTabs } from "../-scheduling/event-types-shell/EventTypeEditorTabs";
import { EventTypeHeaderActions } from "../-scheduling/event-types-shell/EventTypeHeaderActions";
import { getEventTypePublicUrl, isEventTypeTabName } from "../-scheduling/event-types-shell/eventTypeShellTypes";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import {
  eventTypeToPayload,
  eventTypeToUpdatePayload,
  isEventTypePayloadSubmittable,
  type EventTypePayload
} from "../-scheduling/schedulingTypes";

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
  const { data: schedulingProfile } = api.useQuery("get", "/api/scheduling/profile");
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
      subtitle={
        eventType
          ? getEventTypePublicUrl(eventType, schedulingProfile?.handle)
          : t`Edit booking setup for this appointment type.`
      }
      maxWidth="80rem"
      titleContent={
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex items-center gap-3">
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={t`Back`}
              onClick={() =>
                navigate({ to: "/event-types", search: { dialog: undefined, duplicateEventTypeId: undefined } })
              }
            >
              <ArrowLeftIcon />
            </Button>
            <span className="truncate">{eventType?.title ?? t`Event type`}</span>
          </div>
        </div>
      }
      actions={
        eventType && draft ? (
          <EventTypeHeaderActions
            eventType={eventType}
            draft={draft}
            publicHandle={schedulingProfile?.handle}
            canSave={canSave}
            isSaving={updateEventTypeMutation.isPending}
            onDraftChange={handleDraftChange}
            onDelete={() => setDeleteDialogOpen(true)}
          />
        ) : null
      }
    >
      <div data-testid="event-type-layout" className="flex flex-col gap-4">
        <GeneralApiErrors error={updateEventTypeMutation.error} />
        {isLoading || !draft || !eventType ? (
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            <Trans>Loading event type...</Trans>
          </div>
        ) : (
          <>
            <div className="flex items-center justify-between gap-3 rounded-md border p-3 lg:hidden">
              <span className="text-sm font-medium">
                <Trans>Hidden</Trans>
              </span>
              <Switch checked={draft.hidden} onCheckedChange={(hidden) => handleDraftChange({ ...draft, hidden })} />
            </div>
            <EventTypeEditorTabs
              eventTypeId={eventTypeId}
              tabName={tabName}
              draft={draft}
              schedules={schedules}
              canSave={canSave}
              error={updateEventTypeMutation.error}
              onChange={handleDraftChange}
              onSubmit={() =>
                updateEventTypeMutation.mutate({
                  params: { path: { id: eventTypeId } },
                  body: eventTypeToUpdatePayload(eventTypeId, draft)
                })
              }
            />
          </>
        )}
      </div>
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

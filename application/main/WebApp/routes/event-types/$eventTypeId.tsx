import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, Trash2Icon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { EventTypeForm } from "../-scheduling/EventTypeForm";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { eventTypeToPayload, type EventTypePayload } from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/event-types/$eventTypeId")({
  staticData: { trackingTitle: "Event type details" },
  component: EventTypeDetailsPage
});

function EventTypeDetailsPage() {
  const { eventTypeId } = Route.useParams();
  const navigate = useNavigate();
  const { data: eventType, isLoading } = api.useQuery("get", "/api/event-types/{id}", {
    params: { path: { id: eventTypeId } }
  });
  const { data: schedulesData } = api.useQuery("get", "/api/schedules");
  const schedules = schedulesData?.schedules ?? [];
  const [draft, setDraft] = useState<EventTypePayload | null>(null);

  useEffect(() => {
    if (eventType) setDraft(eventTypeToPayload(eventType));
  }, [eventType]);

  const updateEventTypeMutation = api.useMutation("put", "/api/event-types/{id}", {
    onSuccess: () => {
      toast.success(t`Event type updated`);
    }
  });
  const deleteEventTypeMutation = api.useMutation("delete", "/api/event-types/{id}", {
    onSuccess: () => {
      toast.success(t`Event type deleted`);
      navigate({ to: "/event-types" });
    }
  });

  return (
    <SchedulingPageShell
      title={eventType?.title ?? t`Event type`}
      subtitle={t`Edit booking setup for this appointment type.`}
    >
      <div className="mb-6 flex items-center justify-between gap-3">
        <Button variant="ghost" onClick={() => navigate({ to: "/event-types" })}>
          <ArrowLeftIcon />
          <Trans>Back</Trans>
        </Button>
        <Button
          variant="destructive"
          onClick={() => deleteEventTypeMutation.mutate({ params: { path: { id: eventTypeId } } })}
          isPending={deleteEventTypeMutation.isPending}
        >
          <Trash2Icon />
          <Trans>Delete</Trans>
        </Button>
      </div>
      {!isLoading && draft && (
        <EventTypeForm
          value={draft}
          schedules={schedules}
          onChange={setDraft}
          onSubmit={(body) =>
            updateEventTypeMutation.mutate({
              params: { path: { id: eventTypeId } },
              body: { ...body, id: eventTypeId }
            })
          }
          error={updateEventTypeMutation.error}
          isPending={updateEventTypeMutation.isPending}
          submitLabel={t`Save event type`}
        />
      )}
    </SchedulingPageShell>
  );
}

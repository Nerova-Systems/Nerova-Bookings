import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Link as RouterLink, createFileRoute } from "@tanstack/react-router";
import { EyeOffIcon, PlusIcon, TimerIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { EventTypeForm } from "../-scheduling/EventTypeForm";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { newEventTypePayload, type EventTypePayload } from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/event-types/")({
  staticData: { trackingTitle: "Event types" },
  component: EventTypesPage
});

function EventTypesPage() {
  const { data: eventTypesData, isLoading, refetch } = api.useQuery("get", "/api/event-types");
  const { data: schedulesData } = api.useQuery("get", "/api/schedules");
  const schedules = schedulesData?.schedules ?? [];
  const firstScheduleId = schedules[0]?.id;
  const [draft, setDraft] = useState<EventTypePayload>(() => newEventTypePayload(""));

  useEffect(() => {
    if (!draft.scheduleId && firstScheduleId) {
      setDraft((current) => ({ ...current, scheduleId: firstScheduleId }));
    }
  }, [draft.scheduleId, firstScheduleId]);

  const createEventTypeMutation = api.useMutation("post", "/api/event-types", {
    onSuccess: async () => {
      toast.success(t`Event type created`);
      setDraft(newEventTypePayload(firstScheduleId ?? ""));
      await refetch();
    }
  });

  const eventTypes = eventTypesData?.eventTypes ?? [];

  return (
    <SchedulingPageShell title={t`Event types`} subtitle={t`Configure the appointment types clients can book.`}>
      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_26rem]">
        <section className="flex min-w-0 flex-col gap-3">
          {isLoading ? null : eventTypes.length === 0 ? (
            <div className="flex min-h-48 flex-col items-center justify-center gap-3 rounded-md border border-dashed text-center">
              <TimerIcon className="size-8 text-muted-foreground" />
              <div>
                <h2 className="text-base font-medium">
                  <Trans>No event types yet</Trans>
                </h2>
                <p className="text-sm text-muted-foreground">
                  <Trans>Create a private setup event type before opening public booking.</Trans>
                </p>
              </div>
            </div>
          ) : (
            eventTypes.map((eventType) => (
              <RouterLink
                key={eventType.id}
                to="/event-types/$eventTypeId"
                params={{ eventTypeId: eventType.id }}
                className="grid gap-2 rounded-md border p-4 transition-colors hover:bg-muted/60 md:grid-cols-[1fr_auto]"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="truncate text-base font-medium">{eventType.title}</h2>
                    {eventType.hidden && <EyeOffIcon className="size-4 text-muted-foreground" />}
                  </div>
                  <p className="truncate text-sm text-muted-foreground">/{eventType.slug}</p>
                </div>
                <div className="text-sm text-muted-foreground">
                  <Trans>{eventType.durationMinutes} minutes</Trans>
                </div>
              </RouterLink>
            ))
          )}
        </section>
        <aside className="flex flex-col gap-4">
          <div className="flex items-center gap-2">
            <PlusIcon className="size-4" />
            <h2 className="text-base font-medium">
              <Trans>New event type</Trans>
            </h2>
          </div>
          {schedules.length === 0 ? (
            <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
              <Trans>Create an availability schedule before creating event types.</Trans>
            </div>
          ) : (
            <EventTypeForm
              value={draft}
              schedules={schedules}
              onChange={setDraft}
              onSubmit={(body) => createEventTypeMutation.mutate({ body })}
              error={createEventTypeMutation.error}
              isPending={createEventTypeMutation.isPending}
              submitLabel={t`Create event type`}
            />
          )}
        </aside>
      </div>
    </SchedulingPageShell>
  );
}

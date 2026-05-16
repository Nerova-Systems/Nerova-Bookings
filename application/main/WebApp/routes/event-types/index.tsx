import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Link as RouterLink, createFileRoute, useNavigate } from "@tanstack/react-router";
import { CalendarDaysIcon, EyeOffIcon, TimerIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

import { CreateEventTypeDialog } from "../-scheduling/CreateEventTypeDialog";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";

export const Route = createFileRoute("/event-types/")({
  staticData: { trackingTitle: "Event types" },
  component: EventTypesPage
});

function EventTypesPage() {
  const navigate = useNavigate();
  const { data: eventTypesData, isLoading } = api.useQuery("get", "/api/event-types");
  const { data: schedulesData } = api.useQuery("get", "/api/schedules");
  const schedules = schedulesData?.schedules ?? [];
  const scheduleNameById = new Map(schedules.map((schedule) => [schedule.id, schedule.name]));
  const eventTypes = eventTypesData?.eventTypes ?? [];
  const hasSchedules = schedules.length > 0;

  return (
    <SchedulingPageShell
      title={t`Event types`}
      subtitle={t`Configure the appointment types clients can book.`}
      actions={
        hasSchedules ? (
          <CreateEventTypeDialog schedules={schedules} />
        ) : (
          <Button onClick={() => navigate({ to: "/availability" })}>
            <CalendarDaysIcon />
            <Trans>Create availability</Trans>
          </Button>
        )
      }
    >
      {!hasSchedules && (
        <div className="mb-4 rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          <div className="font-medium text-foreground">
            <Trans>Create availability first</Trans>
          </div>
          <p className="mt-1">
            <Trans>Create an availability schedule before creating event types.</Trans>
          </p>
        </div>
      )}
      <section className="flex min-w-0 flex-col">
        {isLoading ? (
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            <Trans>Loading event types...</Trans>
          </div>
        ) : eventTypes.length === 0 ? (
          <Empty className="min-h-48 border">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <TimerIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No event types yet</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Create a private setup event type before opening public booking.</Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="overflow-hidden rounded-md border">
            {eventTypes.map((eventType) => (
              <RouterLink
                key={eventType.id}
                to="/event-types/$eventTypeId"
                params={{ eventTypeId: eventType.id }}
                className="grid gap-2 border-b p-4 transition-colors last:border-b-0 hover:bg-muted/60 md:grid-cols-[1fr_auto]"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="truncate text-base font-medium">{eventType.title}</h2>
                    {eventType.hidden && <EyeOffIcon className="size-4 text-muted-foreground" />}
                  </div>
                  <p className="truncate text-sm text-muted-foreground">/{eventType.slug}</p>
                  <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
                    <span>
                      <Trans>{eventType.durationMinutes} minutes</Trans>
                    </span>
                    <span>{scheduleNameById.get(eventType.scheduleId) ?? t`Schedule unavailable`}</span>
                  </div>
                </div>
                <div className="text-sm text-muted-foreground">{eventType.hidden ? t`Hidden` : t`Visible`}</div>
              </RouterLink>
            ))}
          </div>
        )}
      </section>
    </SchedulingPageShell>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { CalendarDaysIcon, PlusIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { api } from "@/shared/lib/api/client";

import { CreateEventTypeDialog } from "../-scheduling/CreateEventTypeDialog";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { DeleteEventTypeDialog } from "../-scheduling/event-types-shell/DeleteEventTypeDialog";
import { DuplicateEventTypeDialog } from "../-scheduling/event-types-shell/DuplicateEventTypeDialog";
import { EventTypesList } from "../-scheduling/event-types-shell/EventTypesList";
import type { EventType } from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/event-types/")({
  staticData: { trackingTitle: "Event types" },
  validateSearch: (search: Record<string, unknown>) => ({
    dialog: search.dialog === "new" || search.dialog === "duplicate" ? search.dialog : undefined,
    duplicateEventTypeId: typeof search.duplicateEventTypeId === "string" ? search.duplicateEventTypeId : undefined
  }),
  component: EventTypesPage
});

function EventTypesPage() {
  const navigate = useNavigate();
  const search = Route.useSearch();
  const { data: eventTypesData, isLoading } = api.useQuery("get", "/api/event-types");
  const { data: schedulesData } = api.useQuery("get", "/api/schedules");
  const schedules = schedulesData?.schedules ?? [];
  const eventTypes = eventTypesData?.eventTypes ?? [];
  const hasSchedules = schedules.length > 0;
  const duplicateEventType = useMemo(
    () => eventTypes.find((eventType) => eventType.id === search.duplicateEventTypeId) ?? null,
    [eventTypes, search.duplicateEventTypeId]
  );
  const [deleteEventType, setDeleteEventType] = useState<EventType | null>(null);

  const openCreateDialog = () =>
    navigate({ to: "/event-types", search: { dialog: "new", duplicateEventTypeId: undefined } });
  const closeDialog = () =>
    navigate({ to: "/event-types", search: { dialog: undefined, duplicateEventTypeId: undefined } });

  return (
    <SchedulingPageShell
      title={t`Event types`}
      subtitle={t`Configure the appointment types clients can book.`}
      actions={
        hasSchedules ? (
          <Button onClick={openCreateDialog}>
            <PlusIcon />
            <Trans>New event type</Trans>
          </Button>
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
      <EventTypesList
        eventTypes={eventTypes}
        schedules={schedules}
        isLoading={isLoading}
        onDuplicate={(eventType) =>
          navigate({
            to: "/event-types",
            search: { dialog: "duplicate", duplicateEventTypeId: eventType.id }
          })
        }
        onDelete={setDeleteEventType}
      />
      <CreateEventTypeDialog
        schedules={schedules}
        showTrigger={false}
        isOpen={search.dialog === "new"}
        onOpenChange={(isOpen) => {
          if (!isOpen) closeDialog();
        }}
      />
      <DuplicateEventTypeDialog
        eventType={duplicateEventType}
        isOpen={search.dialog === "duplicate" && duplicateEventType !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) closeDialog();
        }}
      />
      <DeleteEventTypeDialog
        eventType={deleteEventType}
        isOpen={deleteEventType !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) setDeleteEventType(null);
        }}
      />
    </SchedulingPageShell>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, Trash2Icon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { ScheduleForm } from "../-scheduling/ScheduleForm";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { scheduleToPayload, type SchedulePayload } from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/availability/$scheduleId")({
  staticData: { trackingTitle: "Availability details" },
  component: AvailabilityDetailsPage
});

function AvailabilityDetailsPage() {
  const { scheduleId } = Route.useParams();
  const navigate = useNavigate();
  const { data: schedule, isLoading } = api.useQuery("get", "/api/schedules/{id}", {
    params: { path: { id: scheduleId } }
  });
  const { data: schedulesData } = api.useQuery("get", "/api/schedules");
  const { data: eventTypesData } = api.useQuery("get", "/api/event-types");
  const schedules = schedulesData?.schedules ?? [];
  const eventTypes = eventTypesData?.eventTypes ?? [];
  const [draft, setDraft] = useState<SchedulePayload | null>(null);
  const isOnlySchedule = schedules.length <= 1;
  const isReferencedByEventType = eventTypes.some((eventType) => eventType.scheduleId === scheduleId);
  const canUnsetDefault =
    !schedule?.isDefault || schedules.some((candidate) => candidate.id !== scheduleId && candidate.isDefault);
  const deleteBlockedReason = isOnlySchedule
    ? t`Create another schedule before deleting this one.`
    : schedule?.isDefault
      ? t`Make another schedule default before deleting this one.`
      : isReferencedByEventType
        ? t`Move event types to another schedule before deleting this one.`
        : null;

  useEffect(() => {
    if (schedule) setDraft(scheduleToPayload(schedule));
  }, [schedule]);

  const updateScheduleMutation = api.useMutation("put", "/api/schedules/{id}", {
    onSuccess: () => {
      toast.success(t`Schedule updated`);
    }
  });
  const deleteScheduleMutation = api.useMutation("delete", "/api/schedules/{id}", {
    onSuccess: () => {
      toast.success(t`Schedule deleted`);
      navigate({ to: "/availability" });
    }
  });

  return (
    <SchedulingPageShell
      title={schedule?.name ?? t`Availability`}
      subtitle={t`Edit the weekly windows attached to this schedule.`}
    >
      <div className="mb-6 flex items-center justify-between gap-3">
        <Button variant="ghost" onClick={() => navigate({ to: "/availability" })}>
          <ArrowLeftIcon />
          <Trans>Back</Trans>
        </Button>
        <Button
          variant="destructive"
          onClick={() => deleteScheduleMutation.mutate({ params: { path: { id: scheduleId } } })}
          isPending={deleteScheduleMutation.isPending}
          disabled={deleteBlockedReason !== null}
        >
          <Trash2Icon />
          <Trans>Delete</Trans>
        </Button>
      </div>
      {deleteBlockedReason && (
        <div className="mb-6 rounded-md border border-dashed p-4 text-sm text-muted-foreground">
          {deleteBlockedReason}
        </div>
      )}
      <GeneralApiErrors error={deleteScheduleMutation.error} />
      {!isLoading && draft && (
        <ScheduleForm
          value={draft}
          onChange={setDraft}
          onSubmit={(body) =>
            updateScheduleMutation.mutate({ params: { path: { id: scheduleId } }, body: { ...body, id: scheduleId } })
          }
          error={updateScheduleMutation.error}
          isPending={updateScheduleMutation.isPending}
          submitLabel={t`Save schedule`}
          canUnsetDefault={canUnsetDefault}
        />
      )}
    </SchedulingPageShell>
  );
}

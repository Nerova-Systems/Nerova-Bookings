import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, Trash2Icon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

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
  const [draft, setDraft] = useState<SchedulePayload | null>(null);

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
        >
          <Trash2Icon />
          <Trans>Delete</Trans>
        </Button>
      </div>
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
        />
      )}
    </SchedulingPageShell>
  );
}

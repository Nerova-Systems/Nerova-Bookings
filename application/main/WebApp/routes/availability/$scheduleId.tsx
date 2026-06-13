import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { Switch } from "@repo/ui/components/Switch";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, PencilIcon, SaveIcon, Trash2Icon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { ScheduleForm } from "../-scheduling/ScheduleForm";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import {
  formatAvailabilityWindows,
  isSchedulePayloadSubmittable,
  scheduleToPayload,
  type SchedulePayload
} from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/availability/$scheduleId")({
  staticData: { trackingTitle: "Hours details" },
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
  const weekdayLabels = {
    0: t`Sun`,
    1: t`Mon`,
    2: t`Tue`,
    3: t`Wed`,
    4: t`Thu`,
    5: t`Fri`,
    6: t`Sat`
  };
  const isOnlySchedule = schedules.length <= 1;
  const isReferencedByEventType = eventTypes.some((eventType) => eventType.scheduleId === scheduleId);
  const canUnsetDefault =
    !schedule?.isDefault || schedules.some((candidate) => candidate.id !== scheduleId && candidate.isDefault);
  const canSubmit = draft ? isSchedulePayloadSubmittable(draft) : false;
  const subtitle = formatAvailabilityWindows(
    draft?.availabilityWindows ?? schedule?.availabilityWindows ?? [],
    weekdayLabels,
    t`No weekly hours`
  );
  const deleteBlockedReason = isOnlySchedule
    ? t`Create another schedule before deleting this one.`
    : schedule?.isDefault
      ? t`Make another schedule default before deleting this one.`
      : isReferencedByEventType
        ? t`Move services to another schedule before deleting this one.`
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
      title={draft?.name || schedule?.name || t`Hours`}
      titleContent={
        <div className="flex min-w-0 items-center gap-3">
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={t`Back`}
            onClick={() => navigate({ to: "/availability" })}
          >
            <ArrowLeftIcon />
          </Button>
          {draft ? (
            <div className="flex min-w-0 items-center gap-2">
              <Input
                aria-label={t`Schedule name`}
                value={draft.name}
                className="h-auto w-[min(28rem,60vw)] min-w-0 border-0 bg-transparent p-0 text-xl font-semibold shadow-none outline-none focus-visible:ring-0 focus-visible:outline-none md:text-2xl dark:bg-transparent"
                onChange={(event) => setDraft({ ...draft, name: event.target.value })}
              />
              <PencilIcon className="size-4 shrink-0 text-muted-foreground" />
            </div>
          ) : (
            <span>{schedule?.name ?? t`Hours`}</span>
          )}
        </div>
      }
      subtitle={subtitle}
      actions={
        draft ? (
          <div className="flex flex-wrap items-center justify-end gap-3">
            <div className="flex items-center gap-2 text-sm font-medium">
              <span id="set-default-schedule-label">
                <Trans>Set as default</Trans>
              </span>
              <Switch
                aria-labelledby="set-default-schedule-label"
                checked={draft.isDefault}
                disabled={draft.isDefault && !canUnsetDefault}
                onCheckedChange={(isDefault) => setDraft({ ...draft, isDefault })}
              />
            </div>
            <div className="hidden h-5 border-l sm:block" />
            <Button
              type="button"
              variant="destructive"
              size="icon"
              aria-label={t`Delete schedule`}
              onClick={() => deleteScheduleMutation.mutate({ params: { path: { id: scheduleId } } })}
              isPending={deleteScheduleMutation.isPending}
              disabled={deleteBlockedReason !== null}
            >
              <Trash2Icon />
            </Button>
            <div className="hidden h-5 border-l sm:block" />
            <Button
              type="submit"
              form="availability-form"
              isPending={updateScheduleMutation.isPending}
              disabled={!canSubmit}
            >
              <SaveIcon />
              <Trans>Save</Trans>
            </Button>
          </div>
        ) : null
      }
      maxWidth="88rem"
    >
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
          showSubmit={false}
          onTroubleshoot={() => navigate({ to: "/availability/troubleshoot" })}
        />
      )}
    </SchedulingPageShell>
  );
}

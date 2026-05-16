import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Link as RouterLink, createFileRoute } from "@tanstack/react-router";
import { CalendarDaysIcon, PlusIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { ScheduleForm } from "../-scheduling/ScheduleForm";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { newSchedulePayload, type SchedulePayload } from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/availability/")({
  staticData: { trackingTitle: "Availability" },
  component: AvailabilityPage
});

function AvailabilityPage() {
  const [draft, setDraft] = useState<SchedulePayload>(() => newSchedulePayload());
  const { data, isLoading, refetch } = api.useQuery("get", "/api/schedules");
  const createScheduleMutation = api.useMutation("post", "/api/schedules", {
    onSuccess: async () => {
      toast.success(t`Schedule created`);
      setDraft(newSchedulePayload());
      await refetch();
    }
  });

  const schedules = data?.schedules ?? [];

  return (
    <SchedulingPageShell title={t`Availability`} subtitle={t`Manage weekly schedules for booking availability.`}>
      <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_24rem]">
        <section className="flex min-w-0 flex-col gap-3">
          {isLoading ? null : schedules.length === 0 ? (
            <div className="flex min-h-48 flex-col items-center justify-center gap-3 rounded-md border border-dashed text-center">
              <CalendarDaysIcon className="size-8 text-muted-foreground" />
              <div>
                <h2 className="text-base font-medium">
                  <Trans>No schedules yet</Trans>
                </h2>
                <p className="text-sm text-muted-foreground">
                  <Trans>Create a weekly schedule before publishing event types.</Trans>
                </p>
              </div>
            </div>
          ) : (
            schedules.map((schedule) => (
              <RouterLink
                key={schedule.id}
                to="/availability/$scheduleId"
                params={{ scheduleId: schedule.id }}
                className="grid gap-2 rounded-md border p-4 transition-colors hover:bg-muted/60 md:grid-cols-[1fr_auto]"
              >
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h2 className="truncate text-base font-medium">{schedule.name}</h2>
                    {schedule.isDefault && (
                      <span className="rounded-md bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
                        <Trans>Default</Trans>
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-muted-foreground">{schedule.timeZone}</p>
                </div>
                <div className="text-sm text-muted-foreground">
                  <Trans>{schedule.availabilityWindows.length} windows</Trans>
                </div>
              </RouterLink>
            ))
          )}
        </section>
        <aside className="flex flex-col gap-4">
          <div className="flex items-center gap-2">
            <PlusIcon className="size-4" />
            <h2 className="text-base font-medium">
              <Trans>New schedule</Trans>
            </h2>
          </div>
          <ScheduleForm
            value={draft}
            onChange={setDraft}
            onSubmit={(body) => createScheduleMutation.mutate({ body })}
            error={createScheduleMutation.error}
            isPending={createScheduleMutation.isPending}
            submitLabel={t`Create schedule`}
          />
        </aside>
      </div>
    </SchedulingPageShell>
  );
}

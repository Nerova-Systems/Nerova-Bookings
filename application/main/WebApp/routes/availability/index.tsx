import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Link as RouterLink, createFileRoute } from "@tanstack/react-router";
import { CalendarDaysIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

import { CreateScheduleDialog } from "../-scheduling/CreateScheduleDialog";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { formatAvailabilityWindows } from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/availability/")({
  staticData: { trackingTitle: "Availability" },
  component: AvailabilityPage
});

function AvailabilityPage() {
  const { data, isLoading } = api.useQuery("get", "/api/schedules");
  const schedules = data?.schedules ?? [];
  const weekdayLabels = {
    0: t`Sunday`,
    1: t`Monday`,
    2: t`Tuesday`,
    3: t`Wednesday`,
    4: t`Thursday`,
    5: t`Friday`,
    6: t`Saturday`
  };

  return (
    <SchedulingPageShell
      title={t`Availability`}
      subtitle={t`Manage weekly schedules for booking availability.`}
      actions={<CreateScheduleDialog isFirstSchedule={schedules.length === 0} />}
    >
      <section className="flex min-w-0 flex-col">
        {isLoading ? (
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            <Trans>Loading schedules...</Trans>
          </div>
        ) : schedules.length === 0 ? (
          <Empty className="min-h-48 border">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <CalendarDaysIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No schedules yet</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Create a weekly schedule before publishing event types.</Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="overflow-hidden rounded-md border">
            {schedules.map((schedule) => (
              <RouterLink
                key={schedule.id}
                to="/availability/$scheduleId"
                params={{ scheduleId: schedule.id }}
                className="grid gap-2 border-b p-4 transition-colors last:border-b-0 hover:bg-muted/60 md:grid-cols-[1fr_auto]"
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
                  <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
                    <span>{schedule.timeZone}</span>
                    <span>
                      {formatAvailabilityWindows(
                        schedule.availabilityWindows,
                        weekdayLabels,
                        t`No availability windows`
                      )}
                    </span>
                  </div>
                </div>
                <div className="text-sm text-muted-foreground">
                  <Trans>{schedule.availabilityWindows.length} windows</Trans>
                </div>
              </RouterLink>
            ))}
          </div>
        )}
      </section>
    </SchedulingPageShell>
  );
}

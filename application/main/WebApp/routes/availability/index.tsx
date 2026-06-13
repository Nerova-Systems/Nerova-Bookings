import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Link as RouterLink, createFileRoute } from "@tanstack/react-router";
import { CalendarDaysIcon } from "lucide-react";

import { api } from "@/shared/lib/api/client";

import { CreateScheduleDialog } from "../-scheduling/CreateScheduleDialog";
import { SchedulingPageShell } from "../-scheduling/SchedulingPageShell";
import { formatAvailabilityWindows, formatMinutes, type Schedule } from "../-scheduling/schedulingTypes";

export const Route = createFileRoute("/availability/")({
  staticData: { trackingTitle: "Hours" },
  component: AvailabilityPage
});

function AvailabilityPage() {
  const { data, isLoading } = api.useQuery("get", "/api/schedules");
  const schedules = data?.schedules ?? [];
  const primarySchedule = schedules.find((schedule) => schedule.isDefault) ?? schedules[0];
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
      title={t`Hours`}
      subtitle={t`When are you open for client bookings?`}
      actions={<CreateScheduleDialog isFirstSchedule={schedules.length === 0} />}
    >
      <section className="flex min-w-0 flex-col gap-4">
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
                <Trans>Create a weekly schedule before publishing services.</Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <>
            {primarySchedule && <WeeklyHoursCard schedule={primarySchedule} />}
            <div>
              <h2 className="mb-2 text-sm font-medium">
                <Trans>Schedules</Trans>
              </h2>
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
                        <h3 className="truncate text-base font-medium">{schedule.name}</h3>
                        {schedule.isDefault && (
                          <Badge variant="secondary">
                            <Trans>Default</Trans>
                          </Badge>
                        )}
                      </div>
                      <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
                        <span>{schedule.timeZone}</span>
                        <span>
                          {formatAvailabilityWindows(schedule.availabilityWindows, weekdayLabels, t`No hours set`)}
                        </span>
                      </div>
                    </div>
                    <div className="text-sm text-muted-foreground">
                      <Trans>Edit hours</Trans>
                    </div>
                  </RouterLink>
                ))}
              </div>
            </div>
          </>
        )}
      </section>
    </SchedulingPageShell>
  );
}

function WeeklyHoursCard({ schedule }: Readonly<{ schedule: Schedule }>) {
  const days = [
    { value: 0, label: t`Sunday` },
    { value: 1, label: t`Monday` },
    { value: 2, label: t`Tuesday` },
    { value: 3, label: t`Wednesday` },
    { value: 4, label: t`Thursday` },
    { value: 5, label: t`Friday` },
    { value: 6, label: t`Saturday` }
  ];

  return (
    <div className="rounded-md border bg-background p-4">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-medium">
            <Trans>When are you open?</Trans>
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            <Trans>{schedule.name} controls when clients can book.</Trans>
          </p>
        </div>
        <Badge variant="secondary">{schedule.timeZone}</Badge>
      </div>
      <div className="divide-y rounded-md border">
        {days.map((day) => {
          const ranges = schedule.availabilityWindows
            .filter((window) => window.days.includes(day.value))
            .map((window) => `${formatMinutes(window.startMinute)} - ${formatMinutes(window.endMinute)}`);

          return (
            <div key={day.value} className="grid gap-2 p-3 sm:grid-cols-[9rem_1fr]">
              <div className="text-sm font-medium">{day.label}</div>
              <div className="flex flex-wrap gap-2 text-sm">
                {ranges.length === 0 ? (
                  <span className="text-muted-foreground">
                    <Trans>Closed</Trans>
                  </span>
                ) : (
                  ranges.map((range) => (
                    <Badge key={range} variant="outline">
                      {range}
                    </Badge>
                  ))
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

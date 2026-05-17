import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { FormValidationContext } from "@repo/ui/components/Form";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { formatAvailabilityWindows } from "../schedulingTypes";
import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeAvailabilityTab({ value, schedules, onChange, error }: EventTypeTabProps) {
  const selectedSchedule = schedules.find((schedule) => schedule.id === value.scheduleId);
  const scheduleItems = schedules.map((schedule) => ({ value: schedule.id, label: schedule.name }));
  const weekdayLabels = {
    0: t`Sun`,
    1: t`Mon`,
    2: t`Tue`,
    3: t`Wed`,
    4: t`Thu`,
    5: t`Fri`,
    6: t`Sat`
  };

  return (
    <FormValidationContext.Provider value={error?.errors ?? {}}>
      <div className="grid gap-5">
        <EventTypeTabSection
          title={<Trans>Availability</Trans>}
          description={<Trans>Choose which schedule controls when this event type can be booked.</Trans>}
        >
          <SelectField
            name="scheduleId"
            label={t`Schedule`}
            items={scheduleItems}
            value={value.scheduleId}
            onValueChange={(scheduleId) => onChange({ ...value, scheduleId: scheduleId ?? "" })}
          >
            <SelectTrigger>
              <SelectValue placeholder={t`Select schedule`}>
                {(scheduleId: string) =>
                  schedules.find((schedule) => schedule.id === scheduleId)?.name ?? t`Select schedule`
                }
              </SelectValue>
            </SelectTrigger>
            <SelectContent>
              {schedules.map((schedule) => (
                <SelectItem key={schedule.id} value={schedule.id}>
                  {schedule.name}
                </SelectItem>
              ))}
            </SelectContent>
          </SelectField>
        </EventTypeTabSection>
        <EventTypeTabSection
          title={<Trans>Schedule summary</Trans>}
          description={<Trans>Review the selected schedule before saving this event type.</Trans>}
        >
          {selectedSchedule ? (
            <ScheduleSummary schedule={selectedSchedule} weekdayLabels={weekdayLabels} />
          ) : (
            <NoSelectedSchedule />
          )}
        </EventTypeTabSection>
      </div>
    </FormValidationContext.Provider>
  );
}

function ScheduleSummary({
  schedule,
  weekdayLabels
}: Readonly<{ schedule: EventTypeTabProps["schedules"][number]; weekdayLabels: Record<number, string> }>) {
  return (
    <div className="grid gap-3 rounded-md border bg-muted/30 p-4">
      <div className="flex items-start justify-between gap-3">
        <div className="grid gap-1">
          <div className="font-medium">{schedule.name}</div>
          <div className="text-sm text-muted-foreground">{schedule.timeZone}</div>
        </div>
        {schedule.isDefault && (
          <Badge variant="secondary">
            <Trans>Default</Trans>
          </Badge>
        )}
      </div>
      <div className="grid gap-1 text-sm">
        <div className="font-medium">
          <Trans>Weekly hours</Trans>
        </div>
        <div className="text-muted-foreground">
          {formatAvailabilityWindows(schedule.availabilityWindows, weekdayLabels, t`No weekly availability`)}
        </div>
      </div>
      <div className="text-sm text-muted-foreground">
        <Trans>Overrides: {schedule.dateOverrides.length}</Trans>
      </div>
    </div>
  );
}

function NoSelectedSchedule() {
  return (
    <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
      <Trans>Select a schedule to see its availability summary.</Trans>
    </div>
  );
}

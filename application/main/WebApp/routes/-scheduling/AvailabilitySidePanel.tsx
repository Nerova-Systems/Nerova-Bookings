import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle,
  DialogTrigger
} from "@repo/ui/components/Dialog";
import { TextField } from "@repo/ui/components/TextField";
import { TimeZonePicker } from "@repo/ui/components/TimeZonePicker";
import { InfoIcon, PlusIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";

import type { AvailabilityDateOverride, SchedulePayload } from "./schedulingTypes";

import { formatMinutes, parseTime } from "./schedulingTypes";

const today = () => new Date().toISOString().slice(0, 10);

export function DateOverridesCard({
  dateOverrides,
  onChange
}: Readonly<{
  dateOverrides: AvailabilityDateOverride[];
  onChange: (dateOverrides: AvailabilityDateOverride[]) => void;
}>) {
  const removeDateOverride = (date: string) =>
    onChange(dateOverrides.filter((dateOverride) => dateOverride.date !== date));

  return (
    <div className="rounded-md border p-6">
      <div className="flex items-center gap-1">
        <h3 className="text-base font-semibold">
          <Trans>Date overrides</Trans>
        </h3>
        <InfoIcon className="size-4 text-muted-foreground" />
      </div>
      <p className="mt-1 text-sm text-muted-foreground">
        <Trans>Add dates when your availability changes from your daily hours.</Trans>
      </p>
      <div className="mt-5 flex flex-col gap-3">
        {dateOverrides.map((dateOverride) => (
          <div key={dateOverride.date} className="flex items-center justify-between gap-3 rounded-md border p-3">
            <div>
              <div className="font-medium">{dateOverride.date}</div>
              <div className="text-sm text-muted-foreground">
                {dateOverride.windows.length === 0
                  ? t`Unavailable`
                  : dateOverride.windows
                      .map((window) => `${formatMinutes(window.startMinute)}-${formatMinutes(window.endMinute)}`)
                      .join(", ")}
              </div>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label={t`Remove date override`}
              onClick={() => removeDateOverride(dateOverride.date)}
            >
              <Trash2Icon />
            </Button>
          </div>
        ))}
        <AddDateOverrideDialog dateOverrides={dateOverrides} onChange={onChange} />
      </div>
    </div>
  );
}

function AddDateOverrideDialog({
  dateOverrides,
  onChange
}: Readonly<{
  dateOverrides: AvailabilityDateOverride[];
  onChange: (dateOverrides: AvailabilityDateOverride[]) => void;
}>) {
  const [open, setOpen] = useState(false);
  const [date, setDate] = useState(today);
  const [startTime, setStartTime] = useState("09:00");
  const [endTime, setEndTime] = useState("17:00");
  const canSubmit = date.trim().length > 0 && parseTime(startTime, -1) < parseTime(endTime, -1);

  const handleOpenChange = (nextOpen: boolean) => {
    if (nextOpen) {
      setDate(today());
      setStartTime("09:00");
      setEndTime("17:00");
    }
    setOpen(nextOpen);
  };

  return (
    <Dialog trackingTitle={t`Add date override`} open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger render={<Button type="button" variant="outline" className="w-fit" />}>
        <PlusIcon />
        <Trans>Add an override</Trans>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          onSubmit={() => {
            if (!canSubmit) return;
            onChange(
              [
                ...dateOverrides.filter((dateOverride) => dateOverride.date !== date),
                {
                  date,
                  windows: [{ startMinute: parseTime(startTime, 540), endMinute: parseTime(endTime, 1020) }]
                }
              ].sort((left, right) => left.date.localeCompare(right.date))
            );
            setOpen(false);
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Add date override</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>Set custom availability for one date.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody>
            <div className="grid gap-4 sm:grid-cols-3">
              <TextField name="date" label={t`Date`} type="date" required={true} value={date} onChange={setDate} />
              <TextField name="startTime" label={t`Start`} value={startTime} onChange={setStartTime} />
              <TextField name="endTime" label={t`End`} value={endTime} onChange={setEndTime} />
            </div>
            {!canSubmit && (
              <p className="text-sm text-destructive">
                <Trans>End time must be after start time.</Trans>
              </p>
            )}
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" disabled={!canSubmit}>
              <Trans>Add override</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}

export function AvailabilitySidePanel({
  value,
  onChange,
  onTroubleshoot
}: Readonly<{ value: SchedulePayload; onChange: (value: SchedulePayload) => void; onTroubleshoot: () => void }>) {
  return (
    <aside className="flex flex-col gap-8">
      <TimeZonePicker
        name="timeZone"
        label={t`Timezone`}
        value={value.timeZone}
        onValueChange={(timeZone) => onChange({ ...value, timeZone: timeZone ?? "UTC" })}
      />
      <div className="border-t pt-8">
        <div className="rounded-md border p-5">
          <h3 className="text-base font-semibold">
            <Trans>Something doesn't look right?</Trans>
          </h3>
          <Button type="button" variant="outline" className="mt-4" onClick={onTroubleshoot}>
            <Trans>Launch troubleshooter</Trans>
          </Button>
        </div>
      </div>
    </aside>
  );
}

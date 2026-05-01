import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { Switch } from "@repo/ui/components/Switch";
import { PlusIcon, Trash2Icon } from "lucide-react";
import type { FormEvent } from "react";
import { toast } from "sonner";

import { useUpdateWeeklyAvailability } from "@/shared/lib/availabilitySettingsApi";

import { updateDay, updateWindow, type DayState } from "./availabilityState";

interface WeeklyAvailabilityFormProps {
  days: DayState[];
  setDays: (days: DayState[]) => void;
}

export function WeeklyAvailabilityForm({ days, setDays }: WeeklyAvailabilityFormProps) {
  const updateAvailability = useUpdateWeeklyAvailability();

  const saveAvailability = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    for (const day of days) {
      if (!day.enabled) continue;
      for (const window of day.windows) {
        if (window.endTime <= window.startTime) {
          toast.error(`${day.dayOfWeek} end time must be after start time.`);
          return;
        }
      }
    }

    updateAvailability.mutate(
      {
        days: days.map((day) => ({
          dayOfWeek: day.dayOfWeek,
          windows: day.enabled ? day.windows : []
        }))
      },
      {
        onSuccess: () => toast.success("Business availability updated."),
        onError: (error) => toast.error(error instanceof Error ? error.message : "Could not update availability.")
      }
    );
  };

  return (
    <form className="flex flex-col gap-3" onSubmit={saveAvailability}>
      <div className="grid gap-2">
        {days.map((day, dayIndex) => (
          <AvailabilityDayRow key={day.dayOfWeek} day={day} dayIndex={dayIndex} days={days} setDays={setDays} />
        ))}
      </div>
      <div className="flex justify-end">
        <Button type="submit" size="sm" isPending={updateAvailability.isPending}>
          Save availability
        </Button>
      </div>
    </form>
  );
}

function AvailabilityDayRow({
  day,
  dayIndex,
  days,
  setDays
}: {
  day: DayState;
  dayIndex: number;
  days: DayState[];
  setDays: (days: DayState[]) => void;
}) {
  return (
    <div className="rounded-lg border border-border p-3">
      <div className="flex items-center gap-3">
        <Switch checked={day.enabled} onCheckedChange={(enabled) => updateDay(days, setDays, dayIndex, { enabled })} />
        <span className="min-w-24 text-sm font-medium">{day.dayOfWeek}</span>
        {!day.enabled && <span className="text-xs text-muted-foreground">Closed</span>}
        <Button
          type="button"
          variant="outline"
          size="xs"
          className="ml-auto"
          disabled={!day.enabled}
          onClick={() =>
            updateDay(days, setDays, dayIndex, {
              windows: [...day.windows, { startTime: "09:00", endTime: "17:00" }]
            })
          }
        >
          <PlusIcon className="size-3" />
          Window
        </Button>
      </div>
      {day.enabled && <WindowRows day={day} dayIndex={dayIndex} days={days} setDays={setDays} />}
    </div>
  );
}

function WindowRows({ day, dayIndex, days, setDays }: { day: DayState; dayIndex: number; days: DayState[]; setDays: (days: DayState[]) => void }) {
  return (
    <div className="mt-3 grid gap-2">
      {day.windows.map((window, windowIndex) => (
        <div key={`${day.dayOfWeek}-${windowIndex}`} className="grid grid-cols-[1fr_1fr_auto] gap-2">
          <Input
            type="time"
            value={window.startTime}
            onChange={(event) => updateWindow(days, setDays, dayIndex, windowIndex, { startTime: event.target.value })}
          />
          <Input
            type="time"
            value={window.endTime}
            onChange={(event) => updateWindow(days, setDays, dayIndex, windowIndex, { endTime: event.target.value })}
          />
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            onClick={() =>
              updateDay(days, setDays, dayIndex, {
                windows: day.windows.filter((_, index) => index !== windowIndex)
              })
            }
          >
            <Trash2Icon className="size-4" />
          </Button>
        </div>
      ))}
    </div>
  );
}

/* eslint-disable max-lines */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { Switch } from "@repo/ui/components/Switch";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, CopyIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { useAppointmentShell } from "@/shared/lib/appointmentsApi";
import { useUpdateWeeklyAvailability } from "@/shared/lib/availabilitySettingsApi";

import { buildInitialDays, updateDay, updateWindow, type DayState } from "../calendar/-components/availabilityState";

export const Route = createFileRoute("/dashboard/availability/$scheduleId")({
  staticData: { trackingTitle: "Edit availability" },
  component: EditAvailabilityPage
});

function EditAvailabilityPage() {
  const navigate = useNavigate();
  const shellQuery = useAppointmentShell();
  const initialDays = useMemo(
    () => buildInitialDays(shellQuery.data?.availabilityRules ?? []),
    [shellQuery.data?.availabilityRules]
  );
  const [days, setDays] = useState<DayState[]>(initialDays);
  const [scheduleName, setScheduleName] = useState("Working hours");
  const updateAvailability = useUpdateWeeklyAvailability();

  useEffect(() => {
    setDays(initialDays);
  }, [initialDays]);

  useEffect(() => {
    document.title = t`Working hours | Nerova`;
  }, []);

  return (
    <main className="flex min-h-0 flex-1 flex-col overflow-y-auto bg-[#0f0f0f] px-7 py-6 text-white">
      <header className="flex flex-wrap items-start gap-4">
        <button
          type="button"
          onClick={() => navigate({ to: "/dashboard/availability" })}
          className="mt-1 flex size-9 items-center justify-center rounded-lg text-white/70 hover:bg-white/[0.08] hover:text-white"
          aria-label="Back to availability"
        >
          <ArrowLeftIcon className="size-5" />
        </button>
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <Input
              value={scheduleName}
              onChange={(event) => setScheduleName(event.target.value)}
              className="h-auto border-0 bg-transparent px-0 py-0 font-display text-3xl font-semibold text-white shadow-none focus-visible:outline-none"
              aria-label="Schedule name"
            />
            <PencilIcon className="size-4 text-white/55" />
          </div>
          <p className="mt-1 text-lg text-white/80">{summarizeDays(days)}</p>
        </div>
        <div className="ml-auto flex items-center gap-4">
          <div className="flex items-center gap-3 text-sm font-semibold">
            <Trans>Set as default</Trans>
            <Switch checked={true} onCheckedChange={() => toast.message("This is already the default schedule.")} />
          </div>
          <div className="h-8 border-l border-white/15" />
          <Button
            variant="outline"
            size="icon-sm"
            className="border-white/15 bg-transparent text-white hover:bg-white/[0.08]"
            onClick={() => toast.message("Deleting the default schedule is disabled.")}
          >
            <Trash2Icon className="size-4" />
          </Button>
        </div>
      </header>

      <div className="mt-9 grid gap-7 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <section className="rounded-xl border border-white/10 bg-[#111] p-5">
          <AvailabilityEditor days={days} setDays={setDays} />
        </section>

        <aside className="space-y-7">
          <section>
            <h2 className="mb-3 text-base font-semibold">
              <Trans>Timezone</Trans>
            </h2>
            <select
              className="h-12 w-full rounded-xl border border-white/15 bg-[#111] px-4 text-base font-semibold text-white outline-none"
              value={shellQuery.data?.profile.timeZone ?? "Africa/Johannesburg"}
              disabled
            >
              <option>Africa/Johannesburg</option>
            </select>
          </section>
        </aside>
      </div>

      <section className="mt-7 rounded-xl border border-white/10 bg-[#111] p-7">
        <h2 className="text-xl font-semibold">
          <Trans>Date overrides</Trans>
        </h2>
        <p className="mt-2 text-base text-white/65">
          <Trans>Add dates when your availability changes from your daily hours.</Trans>
        </p>
        <Button
          variant="outline"
          size="sm"
          className="mt-6 border-white/15 bg-transparent text-white hover:bg-white/[0.08]"
          onClick={() => navigate({ href: "/user/out-of-office" })}
        >
          <Trans>Add an override</Trans>
        </Button>
      </section>

      <div className="sticky bottom-0 mt-7 flex justify-end border-t border-white/10 bg-[#0f0f0f]/95 py-4">
        <Button isPending={updateAvailability.isPending} onClick={() => saveAvailability(days, updateAvailability)}>
          <Trans>Save</Trans>
        </Button>
      </div>
    </main>
  );
}

function AvailabilityEditor({ days, setDays }: { days: DayState[]; setDays: (days: DayState[]) => void }) {
  return (
    <div>
      <div className="grid gap-4">
        {days.map((day, dayIndex) => (
          <div key={day.dayOfWeek} className="grid grid-cols-[10rem_1fr_auto] items-start gap-4 max-lg:grid-cols-1">
            <label className="flex items-center gap-3 pt-2 text-lg font-semibold">
              <Switch
                checked={day.enabled}
                onCheckedChange={(enabled) => updateDay(days, setDays, dayIndex, { enabled })}
              />
              {day.dayOfWeek}
            </label>
            <div className="grid gap-3">
              {day.enabled ? (
                day.windows.map((window, windowIndex) => (
                  <div key={`${day.dayOfWeek}-${windowIndex}`} className="flex flex-wrap items-center gap-3">
                    <Input
                      type="time"
                      value={window.startTime}
                      onChange={(event) =>
                        updateWindow(days, setDays, dayIndex, windowIndex, { startTime: event.target.value })
                      }
                      className="h-10 w-32 border-white/15 bg-[#171717] text-white"
                    />
                    <span className="text-white/55">-</span>
                    <Input
                      type="time"
                      value={window.endTime}
                      onChange={(event) =>
                        updateWindow(days, setDays, dayIndex, windowIndex, { endTime: event.target.value })
                      }
                      className="h-10 w-32 border-white/15 bg-[#171717] text-white"
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      className="text-white/70 hover:bg-white/[0.08] hover:text-white"
                      onClick={() =>
                        updateDay(days, setDays, dayIndex, {
                          windows: [...day.windows, { startTime: window.startTime, endTime: window.endTime }]
                        })
                      }
                    >
                      <CopyIcon className="size-4" />
                    </Button>
                  </div>
                ))
              ) : (
                <div className="pt-2 text-sm text-white/45">
                  <Trans>Closed</Trans>
                </div>
              )}
            </div>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              className="mt-1 text-white/70 hover:bg-white/[0.08] hover:text-white"
              disabled={!day.enabled}
              onClick={() =>
                updateDay(days, setDays, dayIndex, {
                  windows: [...day.windows, { startTime: "09:00", endTime: "17:00" }]
                })
              }
            >
              +
            </Button>
          </div>
        ))}
      </div>
    </div>
  );
}

function summarizeDays(days: DayState[]) {
  const enabled = days.filter((day) => day.enabled);
  if (enabled.length === 0) return "No active hours";
  const firstWindow = enabled[0]?.windows[0];
  const dayText = enabled.map((day) => day.dayOfWeek.slice(0, 3)).join(", ");
  return `${dayText}; ${firstWindow?.startTime ?? "09:00"} - ${firstWindow?.endTime ?? "17:00"}`;
}

function saveAvailability(days: DayState[], updateAvailability: ReturnType<typeof useUpdateWeeklyAvailability>) {
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
      onSuccess: () => toast.success("Working hours saved."),
      onError: (error) => toast.error(error instanceof Error ? error.message : "Could not save working hours.")
    }
  );
}

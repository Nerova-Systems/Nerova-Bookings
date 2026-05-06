/* eslint-disable max-lines */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Input } from "@repo/ui/components/Input";
import { Switch } from "@repo/ui/components/Switch";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, CopyIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { toast } from "sonner";

import {
  useAppointmentShell,
  useCreateCalendarBlock,
  useDeleteCalendarBlock,
  type BusinessClosure,
  type CalendarBlock
} from "@/shared/lib/appointmentsApi";
import { useCreateClosure, useDeleteClosure, useUpdateWeeklyAvailability } from "@/shared/lib/availabilitySettingsApi";

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

      <DateOverridesSection blocks={shellQuery.data?.calendarBlocks ?? []} closures={shellQuery.data?.closures ?? []} />

      <div className="sticky bottom-0 mt-7 flex justify-end border-t border-white/10 bg-[#0f0f0f]/95 py-4">
        <Button isPending={updateAvailability.isPending} onClick={() => saveAvailability(days, updateAvailability)}>
          <Trans>Save</Trans>
        </Button>
      </div>
    </main>
  );
}

function DateOverridesSection({ blocks, closures }: { blocks: CalendarBlock[]; closures: BusinessClosure[] }) {
  const [blockForm, setBlockForm] = useState({ title: "", date: "", start: "09:00", end: "10:00" });
  const [closureForm, setClosureForm] = useState({ startDate: "", endDate: "", label: "" });
  const createCalendarBlock = useCreateCalendarBlock();
  const deleteCalendarBlock = useDeleteCalendarBlock();
  const createClosure = useCreateClosure();
  const deleteClosure = useDeleteClosure();
  const manualBlocks = useMemo(() => blocks.filter((block) => block.type === "manual"), [blocks]);
  const manualClosures = useMemo(() => closures.filter((closure) => closure.type === "manual"), [closures]);

  return (
    <section className="mt-7 rounded-xl border border-white/10 bg-[#111] p-7">
      <div className="mb-5">
        <h2 className="text-lg font-semibold">Date overrides</h2>
        <p className="mt-1 text-sm text-white/55">Block appointment slots or close whole dates for this business.</p>
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <OverridePanel title="Blocked time" description="Hide appointment slots for a specific time window.">
          <form
            className="grid gap-3"
            onSubmit={(event) => saveBlockedTime(event, blockForm, createCalendarBlock, setBlockForm)}
          >
            <LabeledInput
              label="Block title"
              value={blockForm.title}
              placeholder="Staff meeting"
              onChange={(title) => setBlockForm({ ...blockForm, title })}
            />
            <div className="grid gap-3 sm:grid-cols-3">
              <LabeledInput
                label="Block date"
                type="date"
                value={blockForm.date}
                required
                onChange={(date) => setBlockForm({ ...blockForm, date })}
              />
              <LabeledInput
                label="Block start"
                type="time"
                value={blockForm.start}
                required
                onChange={(start) => setBlockForm({ ...blockForm, start })}
              />
              <LabeledInput
                label="Block end"
                type="time"
                value={blockForm.end}
                required
                onChange={(end) => setBlockForm({ ...blockForm, end })}
              />
            </div>
            <Button type="submit" className="justify-self-start" isPending={createCalendarBlock.isPending}>
              Save blocked time
            </Button>
          </form>
          <BlockList blocks={manualBlocks} deleteCalendarBlock={deleteCalendarBlock} />
        </OverridePanel>

        <OverridePanel title="Closed dates" description="Close booking availability for full business dates.">
          <form
            className="grid gap-3"
            onSubmit={(event) => saveClosure(event, closureForm, createClosure, setClosureForm)}
          >
            <LabeledInput
              label="Closure label"
              value={closureForm.label}
              placeholder="Public holiday"
              onChange={(label) => setClosureForm({ ...closureForm, label })}
            />
            <div className="grid gap-3 sm:grid-cols-2">
              <LabeledInput
                label="Start date"
                type="date"
                value={closureForm.startDate}
                required
                onChange={(startDate) => setClosureForm({ ...closureForm, startDate })}
              />
              <LabeledInput
                label="End date"
                type="date"
                value={closureForm.endDate}
                onChange={(endDate) => setClosureForm({ ...closureForm, endDate })}
              />
            </div>
            <Button type="submit" className="justify-self-start" isPending={createClosure.isPending}>
              Save closed date
            </Button>
          </form>
          <ClosureList closures={manualClosures} deleteClosure={deleteClosure} />
        </OverridePanel>
      </div>
    </section>
  );
}

function OverridePanel({ title, description, children }: { title: string; description: string; children: ReactNode }) {
  return (
    <div className="rounded-xl border border-white/10 bg-[#171717] p-5">
      <h3 className="text-base font-semibold">{title}</h3>
      <p className="mt-1 text-sm text-white/55">{description}</p>
      <div className="mt-4 grid gap-4">{children}</div>
    </div>
  );
}

function LabeledInput({
  label,
  value,
  type = "text",
  placeholder,
  required,
  onChange
}: {
  label: string;
  value: string;
  type?: string;
  placeholder?: string;
  required?: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <label className="grid gap-1.5 text-sm font-semibold text-white/80">
      {label}
      <Input
        type={type}
        value={value}
        placeholder={placeholder}
        required={required}
        onChange={(event) => onChange(event.target.value)}
        className="border-white/15 bg-[#101010] text-white"
      />
    </label>
  );
}

function BlockList({
  blocks,
  deleteCalendarBlock
}: {
  blocks: CalendarBlock[];
  deleteCalendarBlock: ReturnType<typeof useDeleteCalendarBlock>;
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-white/10">
      {blocks.length === 0 && <div className="px-3 py-2 text-sm text-white/45">No blocked times.</div>}
      {blocks.map((block) => (
        <div key={block.id} className="flex items-center gap-3 border-b border-white/10 px-3 py-2 last:border-b-0">
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold">{block.title}</div>
            <div className="text-xs text-white/50">{formatBlockDateTime(block)}</div>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            className="ml-auto text-white/70 hover:bg-white/[0.08] hover:text-white"
            aria-label={`Delete blocked time ${block.title}`}
            isPending={deleteCalendarBlock.isPending}
            onClick={() =>
              deleteCalendarBlock.mutate(block.id, {
                onSuccess: () => toast.success("Blocked time removed."),
                onError: (error) =>
                  toast.error(error instanceof Error ? error.message : "Could not remove blocked time.")
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

function ClosureList({
  closures,
  deleteClosure
}: {
  closures: BusinessClosure[];
  deleteClosure: ReturnType<typeof useDeleteClosure>;
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-white/10">
      {closures.length === 0 && <div className="px-3 py-2 text-sm text-white/45">No closed dates.</div>}
      {closures.map((closure) => (
        <div key={closure.id} className="flex items-center gap-3 border-b border-white/10 px-3 py-2 last:border-b-0">
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold">{closure.label}</div>
            <div className="text-xs text-white/50">{formatClosureDateRange(closure)}</div>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            className="ml-auto text-white/70 hover:bg-white/[0.08] hover:text-white"
            aria-label={`Delete closed date ${closure.label}`}
            isPending={deleteClosure.isPending}
            onClick={() =>
              deleteClosure.mutate(closure.id, {
                onSuccess: () => toast.success("Closed date removed."),
                onError: (error) =>
                  toast.error(error instanceof Error ? error.message : "Could not remove closed date.")
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

function saveBlockedTime(
  event: FormEvent<HTMLFormElement>,
  blockForm: { title: string; date: string; start: string; end: string },
  createCalendarBlock: ReturnType<typeof useCreateCalendarBlock>,
  setBlockForm: (form: { title: string; date: string; start: string; end: string }) => void
) {
  event.preventDefault();
  if (!blockForm.date) {
    toast.error("Choose a block date.");
    return;
  }
  if (blockForm.end <= blockForm.start) {
    toast.error("Block end time must be after start time.");
    return;
  }

  createCalendarBlock.mutate(
    {
      title: blockForm.title || "Blocked time",
      startAt: toBusinessDateTimeOffset(blockForm.date, blockForm.start),
      endAt: toBusinessDateTimeOffset(blockForm.date, blockForm.end)
    },
    {
      onSuccess: () => {
        toast.success("Blocked time saved.");
        setBlockForm({ title: "", date: "", start: "09:00", end: "10:00" });
      },
      onError: (error) => toast.error(error instanceof Error ? error.message : "Could not save blocked time.")
    }
  );
}

function saveClosure(
  event: FormEvent<HTMLFormElement>,
  closureForm: { startDate: string; endDate: string; label: string },
  createClosure: ReturnType<typeof useCreateClosure>,
  setClosureForm: (form: { startDate: string; endDate: string; label: string }) => void
) {
  event.preventDefault();
  const endDate = closureForm.endDate || closureForm.startDate;
  if (!closureForm.startDate) {
    toast.error("Choose a closure date.");
    return;
  }
  if (endDate < closureForm.startDate) {
    toast.error("Closure end date must be on or after start date.");
    return;
  }

  createClosure.mutate(
    { startDate: closureForm.startDate, endDate, label: closureForm.label || "Closed" },
    {
      onSuccess: () => {
        toast.success("Closed date saved.");
        setClosureForm({ startDate: "", endDate: "", label: "" });
      },
      onError: (error) => toast.error(error instanceof Error ? error.message : "Could not save closed date.")
    }
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

function toBusinessDateTimeOffset(date: string, time: string) {
  return `${date}T${time}:00+02:00`;
}

function formatBlockDateTime(block: CalendarBlock) {
  return `${formatDateTimeValue(block.startAt)} - ${formatTimeValue(block.endAt)}`;
}

function formatClosureDateRange(closure: BusinessClosure) {
  return closure.startDate === closure.endDate ? closure.startDate : `${closure.startDate} to ${closure.endDate}`;
}

function formatDateTimeValue(value: string) {
  return `${value.slice(0, 10)} ${formatTimeValue(value)}`;
}

function formatTimeValue(value: string) {
  const timeStart = value.indexOf("T") + 1;
  return value.slice(timeStart, timeStart + 5);
}

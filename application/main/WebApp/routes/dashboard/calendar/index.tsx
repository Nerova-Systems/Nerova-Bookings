import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useWeekAvailabilitySlots } from "@/shared/lib/availabilityApi";
import { useAppointmentShell, useCreateCalendarBlock, type Appointment } from "@/shared/lib/appointmentsApi";
import { formatShortDate } from "@/shared/lib/dateFormatting";

import { BlockTimeDialog, type BlockTimeForm } from "./-components/BlockTimeDialog";
import { WeekGrid } from "./-components/WeekGrid";

export const Route = createFileRoute("/dashboard/calendar/")({
  staticData: { trackingTitle: "Calendar" },
  component: CalendarPage
});

const EMPTY_APPOINTMENTS: Appointment[] = [];

function CalendarPage() {
  const shellQuery = useAppointmentShell();
  const appointments = shellQuery.data?.appointments ?? EMPTY_APPOINTMENTS;
  const firstAppointmentStart = shellQuery.data?.appointments[0]?.startAt;
  const physicalServices = (shellQuery.data?.services ?? []).filter((service) => service.mode === "physical" && !service.archived);
  const [selectedServiceId, setSelectedServiceId] = useState<string>();
  const [customWeekStart, setCustomWeekStart] = useState<Date | null>(null);
  const [blockOpen, setBlockOpen] = useState(false);
  const [blockForm, setBlockForm] = useState<BlockTimeForm>({ title: "Blocked time", date: "", start: "12:00", end: "13:00" });
  const createBlock = useCreateCalendarBlock();

  const defaultWeekStart = useMemo(() => {
    const firstDate = firstAppointmentStart ? new Date(firstAppointmentStart) : new Date();
    return startOfWeek(firstDate);
  }, [firstAppointmentStart]);
  const weekStart = customWeekStart ?? defaultWeekStart;
  const weekDates = useMemo(
    () =>
      Array.from({ length: 7 }, (_, index) => {
        const date = new Date(weekStart);
        date.setDate(weekStart.getDate() + index);
        return toDateInputValue(date);
      }),
    [weekStart]
  );
  const selectedService = physicalServices.find((service) => service.id === selectedServiceId) ?? physicalServices[0];
  const availability = useWeekAvailabilitySlots(selectedService?.id, weekDates);

  useEffect(() => {
    document.title = t`Calendar | Nerova`;
  }, []);

  useEffect(() => {
    if (!selectedServiceId && physicalServices[0]) setSelectedServiceId(physicalServices[0].id);
  }, [physicalServices, selectedServiceId]);

  const moveWeek = (offset: number) => {
    const next = new Date(weekStart);
    next.setDate(weekStart.getDate() + offset * 7);
    setCustomWeekStart(next);
  };

  const openBlockDialog = () => {
    setBlockForm((current) => ({ ...current, date: current.date || weekDates[0] }));
    setBlockOpen(true);
  };

  const submitBlock = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const startAt = `${blockForm.date}T${blockForm.start}:00+02:00`;
    const endAt = `${blockForm.date}T${blockForm.end}:00+02:00`;
    if (new Date(endAt) <= new Date(startAt)) {
      toast.error("Block end time must be after start time.");
      return;
    }
    createBlock.mutate(
      { title: blockForm.title, startAt, endAt },
      {
        onSuccess: () => {
          toast.success("Time blocked on the calendar.");
          setBlockOpen(false);
        },
        onError: (error) => toast.error(error instanceof Error ? error.message : "Could not block time.")
      }
    );
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Calendar</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            <Trans>Week view · Africa/Johannesburg</Trans>
          </span>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={openBlockDialog}>
            <Trans>Block time</Trans>
          </Button>
          <Button size="sm" onClick={() => toast.message("Manual booking flow is not wired yet.")}>
            <Trans>New manual booking</Trans>
          </Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-7 py-6">
        <div className="mb-3.5 flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => setCustomWeekStart(null)}>
              <Trans>Today</Trans>
            </Button>
            <div className="flex gap-0.5">
              <button
                type="button"
                onClick={() => moveWeek(-1)}
                className="flex size-7 items-center justify-center rounded-md border border-border bg-transparent text-foreground hover:bg-muted"
              >
                <ChevronLeftIcon className="size-3" />
              </button>
              <button
                type="button"
                onClick={() => moveWeek(1)}
                className="flex size-7 items-center justify-center rounded-md border border-border bg-transparent text-foreground hover:bg-muted"
              >
                <ChevronRightIcon className="size-3" />
              </button>
            </div>
            <span className="pl-1.5 font-display text-base font-semibold">{weekLabel(weekStart)}</span>
          </div>
          <div className="ml-auto flex items-center gap-2">
            {physicalServices.length > 0 && (
              <select
                value={selectedService?.id ?? ""}
                onChange={(event) => setSelectedServiceId(event.target.value)}
                className="h-8 rounded-lg border border-border bg-background px-2.5 text-xs font-medium text-foreground"
              >
                {physicalServices.map((service) => (
                  <option key={service.id} value={service.id}>
                    {service.name} · v{service.latestVersionNumber}
                  </option>
                ))}
              </select>
            )}
            <div className="inline-flex items-center gap-1.5 rounded-full bg-muted px-2.5 py-1 text-xs text-muted-foreground">
              <span className="size-1.5 rounded-full bg-success shadow-[0_0_0_3px_rgba(44,122,79,0.2)]" />
              {availability.isLoading ? <Trans>Loading availability slots</Trans> : <Trans>Live availability and busy blocks</Trans>}
            </div>
            <div className="inline-flex gap-0.5 rounded-lg bg-muted p-0.5">
              {(["Day", "Week", "Month"] as const).map((v) => (
                <button
                  key={v}
                  type="button"
                  className={`rounded-md px-2.5 py-1 text-xs font-medium transition-colors ${
                    v === "Week" ? "bg-background text-foreground shadow-sm" : "text-muted-foreground"
                  }`}
                >
                  {v}
                </button>
              ))}
            </div>
          </div>
        </div>

        <WeekGrid appointments={appointments} slots={availability.slots} blocks={shellQuery.data?.calendarBlocks ?? []} weekStart={weekStart} />
      </div>
      {blockOpen && (
        <BlockTimeDialog form={blockForm} pending={createBlock.isPending} onChange={setBlockForm} onClose={() => setBlockOpen(false)} onSubmit={submitBlock} />
      )}
    </div>
  );
}

function startOfWeek(date: Date) {
  const next = new Date(date);
  const day = next.getDay() === 0 ? 7 : next.getDay();
  next.setDate(next.getDate() - day + 1);
  next.setHours(0, 0, 0, 0);
  return next;
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function weekLabel(weekStart: Date) {
  const weekEnd = new Date(weekStart);
  weekEnd.setDate(weekStart.getDate() + 6);
  return `${formatShortDate(weekStart)} - ${formatShortDate(weekEnd)} ${weekEnd.getFullYear()}`;
}

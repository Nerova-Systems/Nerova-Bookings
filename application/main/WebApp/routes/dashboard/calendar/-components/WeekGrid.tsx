import type { Appointment } from "@/shared/lib/appointmentsApi";

import { formatDayNumber, formatWeekday } from "@/shared/lib/dateFormatting";

type EventType = "confirmed" | "pending" | "sync" | "blocked";

interface CalEvent {
  label: string;
  type: EventType;
  topPct: number;
  heightPct: number;
}

interface DayColumn {
  day: string;
  date: string;
  isToday?: boolean;
  isWeekend?: boolean;
  events: CalEvent[];
  closed?: boolean;
}

const HOURS = ["8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18"];

function eventClasses(type: EventType): string {
  if (type === "confirmed") return "bg-success/10 border-success/30 text-[#1b3a26] dark:text-[#b9e3c5]";
  if (type === "pending") return "bg-warning/10 border-warning/30 text-[#6e3210] dark:text-[#f0c8a5]";
  if (type === "sync") return "bg-muted border-border text-muted-foreground";
  return "bg-black/5 border-border text-muted-foreground";
}

export function WeekGrid({ appointments }: { appointments: Appointment[] }) {
  const days = buildDays(appointments);

  return (
    <>
      <div className="grid h-[38.75rem] grid-cols-[3rem_1fr] overflow-hidden rounded-xl border border-border bg-background">
        <div className="flex flex-col border-r border-border">
          <div className="h-9 shrink-0 border-b border-border" />
          {HOURS.map((h) => (
            <div
              key={h}
              className="flex-1 border-b border-border pt-1 pr-1.5 text-right font-mono text-[10.5px] text-muted-foreground last:border-0"
            >
              {h}
            </div>
          ))}
        </div>
        <div className="grid grid-cols-7">
          {days.map((col) => (
            <div
              key={col.date}
              className={`flex flex-col border-r border-border last:border-0 ${col.isWeekend ? "bg-muted/50" : ""}`}
            >
              <div
                className={`flex h-9 shrink-0 items-baseline gap-1 border-b border-border px-2 py-1.5 text-[11px] tracking-[0.04em] text-muted-foreground uppercase ${col.isToday ? "bg-foreground/4 dark:bg-white/5" : ""}`}
              >
                <span>{col.day}</span>
                <strong
                  className={`font-display text-sm text-foreground ${col.isToday ? "inline-flex size-[1.375rem] items-center justify-center rounded-full bg-foreground text-xs text-background" : ""}`}
                >
                  {col.date}
                </strong>
              </div>
              <div
                className="relative flex-1"
                style={{
                  backgroundImage: `repeating-linear-gradient(to bottom, transparent 0, transparent calc(100% / ${HOURS.length} - 1px), var(--border) calc(100% / ${HOURS.length} - 1px), var(--border) calc(100% / ${HOURS.length}))`,
                  backgroundSize: `100% calc(100% / ${HOURS.length})`
                }}
              >
                {col.closed ? (
                  <div className="flex h-full items-center justify-center text-[11px] text-muted-foreground italic">
                    Closed
                  </div>
                ) : (
                  <>
                    {col.isToday && (
                      <div className="absolute right-0 left-0 z-10 border-t border-destructive" style={{ top: "18%" }}>
                        <span className="absolute -top-[4px] -left-[3px] size-[7px] rounded-full bg-destructive" />
                      </div>
                    )}
                    {col.events.map((ev, i) => (
                      <div
                        key={i}
                        className={`absolute right-1 left-1 cursor-pointer overflow-hidden rounded-[5px] border px-1.5 py-1 text-[10.5px] leading-tight ${eventClasses(ev.type)}`}
                        style={{ top: `${ev.topPct}%`, height: `${ev.heightPct}%` }}
                      >
                        {ev.label}
                      </div>
                    ))}
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="mt-3.5 flex flex-wrap gap-4 text-xs text-muted-foreground">
        {[
          { type: "confirmed" as const, label: "Confirmed" },
          { type: "pending" as const, label: "Awaiting confirmation" },
          { type: "sync" as const, label: "External busy blocks (Google/Microsoft via Nango)" },
          { type: "blocked" as const, label: "Blocked time" }
        ].map((item) => (
          <span key={item.label} className="inline-flex items-center gap-1.5">
            <span className={`size-3 rounded-[3px] border ${eventClasses(item.type)}`} />
            {item.label}
          </span>
        ))}
      </div>
    </>
  );
}

function buildDays(appointments: Appointment[]): DayColumn[] {
  const firstDate = appointments.length > 0 ? new Date(appointments[0].startAt) : new Date();
  const weekStart = startOfWeek(firstDate);
  const todayKey = dateKey(new Date());

  return Array.from({ length: 7 }, (_, index) => {
    const date = new Date(weekStart);
    date.setDate(weekStart.getDate() + index);
    const isWeekend = date.getDay() === 0 || date.getDay() === 6;
    const events = appointments
      .filter((appointment) => dateKey(new Date(appointment.startAt)) === dateKey(date))
      .map(toCalendarEvent);

    if (index === 2) {
      events.push({ label: "External busy · Nango", type: "sync", topPct: 45.8, heightPct: 8.3 });
    }

    return {
      day: formatWeekday(date),
      date: formatDayNumber(date),
      isToday: dateKey(date) === todayKey,
      isWeekend,
      events,
      closed: isWeekend
    };
  });
}

function toCalendarEvent(appointment: Appointment): CalEvent {
  const start = new Date(appointment.startAt);
  const end = new Date(appointment.endAt);
  const minutesFromStart = (start.getHours() - 8) * 60 + start.getMinutes();
  const durationMinutes = Math.max(30, (end.getTime() - start.getTime()) / 60000);
  const type = appointment.status === "confirmed" ? "confirmed" : "pending";

  return {
    label: `${appointment.time} · ${appointment.name} · ${appointment.statusLabel}`,
    type,
    topPct: Math.max(0, Math.min(92, (minutesFromStart / 600) * 100)),
    heightPct: Math.max(4.1, (durationMinutes / 600) * 100)
  };
}

function startOfWeek(date: Date) {
  const next = new Date(date);
  const day = next.getDay() === 0 ? 7 : next.getDay();
  next.setDate(next.getDate() - day + 1);
  next.setHours(0, 0, 0, 0);
  return next;
}

function dateKey(date: Date) {
  return date.toISOString().slice(0, 10);
}

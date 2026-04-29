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

const DAYS: DayColumn[] = [
  {
    day: "Mon",
    date: "21",
    events: [
      { label: "10:00 · Refilwe", type: "confirmed", topPct: 8.3, heightPct: 8.3 },
      { label: "13:00 · Pieter", type: "confirmed", topPct: 33.3, heightPct: 8.3 },
      { label: "Personal · Google", type: "sync", topPct: 50, heightPct: 16.6 }
    ]
  },
  {
    day: "Tue",
    date: "22",
    isToday: true,
    events: [
      { label: "09:00 · Liam · awaiting confirm", type: "pending", topPct: 8.3, heightPct: 16.6 },
      { label: "10:30 · Thandi · 30m", type: "pending", topPct: 20.8, heightPct: 4.1 },
      { label: "13:00 · Pieter", type: "confirmed", topPct: 41.6, heightPct: 8.3 },
      { label: "15:30 · Group · 4 ppl", type: "confirmed", topPct: 62.5, heightPct: 12.5 }
    ]
  },
  {
    day: "Wed",
    date: "23",
    events: [
      { label: "09:30 · Refilwe", type: "pending", topPct: 12.5, heightPct: 8.3 },
      { label: "11:00 · Marco", type: "confirmed", topPct: 25, heightPct: 4.1 },
      { label: "Lunch · Google", type: "sync", topPct: 45.8, heightPct: 8.3 },
      { label: "14:00 · Aisha · pay overdue", type: "pending", topPct: 62.5, heightPct: 8.3 }
    ]
  },
  {
    day: "Thu",
    date: "24",
    events: [
      { label: "Blocked", type: "blocked", topPct: 0, heightPct: 33.3 },
      { label: "14:00 · Mia", type: "confirmed", topPct: 50, heightPct: 8.3 }
    ]
  },
  {
    day: "Fri",
    date: "25",
    events: [
      { label: "10:00 · Sipho", type: "confirmed", topPct: 8.3, heightPct: 8.3 },
      { label: "12:00 · Ayanda", type: "confirmed", topPct: 25, heightPct: 8.3 },
      { label: "15:00 · Olivia", type: "confirmed", topPct: 50, heightPct: 12.5 }
    ]
  },
  { day: "Sat", date: "26", isWeekend: true, events: [], closed: true },
  { day: "Sun", date: "27", isWeekend: true, events: [], closed: true }
];

const HOURS = ["8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18"];

function eventClasses(type: EventType): string {
  if (type === "confirmed") return "bg-success/10 border-success/30 text-[#1b3a26] dark:text-[#b9e3c5]";
  if (type === "pending") return "bg-warning/10 border-warning/30 text-[#6e3210] dark:text-[#f0c8a5]";
  if (type === "sync") return "bg-muted border-border text-muted-foreground";
  return "bg-black/5 border-border text-muted-foreground";
}

export function WeekGrid() {
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
          {DAYS.map((col) => (
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
          { type: "sync" as const, label: "External (Google sync)" },
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

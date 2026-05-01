import type { Appointment, AvailabilityRule, BusinessClosure, CalendarBlock } from "@/shared/lib/appointmentsApi";

import { buildDays, buildHourRange, eventClasses, formatHour, gmtLabel, hourGrid, minutesFromStart } from "./weekGridModel";

export function WeekGrid({
  appointments,
  blocks,
  availabilityRules,
  closures,
  weekStart,
  timeZone,
  selectedAppointmentId,
  onAppointmentSelect
}: {
  appointments: Appointment[];
  blocks: CalendarBlock[];
  availabilityRules: AvailabilityRule[];
  closures: BusinessClosure[];
  weekStart: Date;
  timeZone: string;
  selectedAppointmentId?: string | null;
  onAppointmentSelect?: (appointmentId: string) => void;
}) {
  const range = buildHourRange(appointments, blocks, availabilityRules, weekStart);
  const days = buildDays(appointments, blocks, availabilityRules, closures, weekStart, range);
  const hours = Array.from({ length: range.endHour - range.startHour + 1 }, (_, index) => range.startHour + index);
  const now = new Date();
  const nowTop = ((minutesFromStart(now, range.startHour) / range.totalMinutes) * 100).toFixed(3);

  return (
    <>
      <div className="h-[min(48rem,calc(100vh-13rem))] min-h-[34rem] overflow-hidden rounded-xl border border-border bg-[#111] text-[#d9d9d9] shadow-sm">
        <div className="h-full overflow-y-auto [scrollbar-gutter:stable]">
          <div className="grid grid-cols-[6.125rem_repeat(7,minmax(0,1fr))]">
            <div className="sticky top-0 z-40 flex h-14 items-center border-r border-b border-white/10 bg-[#161616] px-3 text-sm font-medium text-[#bdbdbd]">
              {gmtLabel(timeZone)}
            </div>
            {days.map((day) => (
              <div
                key={day.dateKey}
                className="sticky top-0 z-40 flex h-14 items-center justify-center gap-2 border-r border-b border-white/10 bg-[#111] text-sm font-medium last:border-r-0"
              >
                <span className="text-[#bdbdbd] uppercase">{day.day}</span>
                <span
                  className={
                    day.isToday
                      ? "inline-flex size-8 items-center justify-center rounded-full bg-white text-base font-semibold text-black"
                      : "text-base font-semibold text-[#bdbdbd]"
                  }
                >
                  {day.dayNumber}
                </span>
              </div>
            ))}
            <div className="relative border-r border-white/10 bg-[#161616]" style={{ minHeight: `${range.totalMinutes}px` }}>
            {hours.map((hour) => (
              <div
                key={hour}
                className="absolute right-3 font-medium text-[#a9a9a9]"
                style={{ top: `${(((hour - range.startHour) * 60) / range.totalMinutes) * 100}%`, transform: "translateY(-0.55rem)" }}
              >
                {formatHour(hour)}
              </div>
            ))}
            </div>
            {days.map((day) => (
              <div
                key={day.dateKey}
                className="relative border-r border-white/10 last:border-0"
                style={{
                  minHeight: `${range.totalMinutes}px`,
                  backgroundImage: hourGrid(range.startHour, range.endHour),
                  backgroundSize: `100% ${(60 / range.totalMinutes) * 100}%`
                }}
              >
                {day.bands.map((band, index) => (
                  <div
                    key={`${day.dateKey}-${band.startTime}-${index}`}
                    className="absolute inset-x-1 rounded-md border border-emerald-300/10 bg-emerald-300/[0.055]"
                    style={{ top: `${band.topPct}%`, height: `${band.heightPct}%` }}
                  />
                ))}
                {day.closure && (
                  <div className="absolute inset-x-1 z-20 rounded-md border border-white/10 bg-black/55 px-2 py-2 text-xs font-medium text-[#d7d7d7]" style={{ top: "0.5rem" }}>
                    Closed - {day.closure.label}
                  </div>
                )}
                {!day.closure && day.bands.length === 0 && (
                  <div className="absolute inset-0 flex items-center justify-center text-xs text-[#888]">Closed</div>
                )}
                {day.isToday && minutesFromStart(now, range.startHour) >= 0 && minutesFromStart(now, range.startHour) <= range.totalMinutes && (
                  <div className="absolute right-0 left-0 z-30 border-t border-red-500" style={{ top: `${nowTop}%` }}>
                    <span className="absolute -top-[4px] -left-[4px] size-2 rounded-full bg-red-500" />
                  </div>
                )}
                {day.events.map((event, index) => {
                  const isSelected = event.appointmentId && event.appointmentId === selectedAppointmentId;
                  const className = `absolute right-1.5 left-1.5 overflow-hidden rounded-md border px-2 py-1.5 text-left text-[11px] leading-tight shadow-sm transition-[box-shadow,transform] ${eventClasses(event.type)} ${
                    isSelected ? "ring-2 ring-white" : ""
                  }`;
                  const style = { top: `${event.topPct}%`, height: `${event.heightPct}%`, zIndex: event.zIndex };

                  return event.appointmentId && onAppointmentSelect ? (
                    <button
                      key={`${day.dateKey}-${event.label}-${index}`}
                      type="button"
                      className={className}
                      style={style}
                      onClick={() => onAppointmentSelect(event.appointmentId!)}
                    >
                      {event.label}
                    </button>
                  ) : (
                    <div key={`${day.dateKey}-${event.label}-${index}`} className={className} style={style}>
                      {event.label}
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
        </div>
      </div>

      <div className="mt-3.5 flex flex-wrap gap-4 text-xs text-muted-foreground">
        {[
          { className: "border-emerald-300/10 bg-emerald-300/[0.18]", label: "Business hours" },
          { className: eventClasses("confirmed"), label: "Confirmed" },
          { className: eventClasses("pending"), label: "Awaiting confirmation" },
          { className: eventClasses("sync"), label: "External busy" },
          { className: eventClasses("blocked"), label: "Blocked time" }
        ].map((item) => (
          <span key={item.label} className="inline-flex items-center gap-1.5">
            <span className={`size-3 rounded-[3px] border ${item.className}`} />
            {item.label}
          </span>
        ))}
      </div>
    </>
  );
}

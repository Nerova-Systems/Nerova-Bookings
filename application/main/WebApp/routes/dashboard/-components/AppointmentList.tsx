import { isTodayInTimeZone } from "@/shared/lib/dateFormatting";

import { StatusDot, statusTextClass, type Appointment } from "./appointmentTypes";

export type FilterTab = "all" | "needs-action" | "paid" | "today";

export function AppointmentList({
  selectedId,
  onSelect,
  activeTab,
  onTabChange,
  appointments,
  timeZone
}: {
  selectedId: string;
  onSelect: (id: string) => void;
  activeTab: FilterTab;
  onTabChange: (tab: FilterTab) => void;
  appointments: Appointment[];
  timeZone: string;
}) {
  const needsActionCount = appointments.filter((a) => a.needsAction).length;

  const filtered = appointments.filter((a) => {
    if (activeTab === "needs-action") return a.needsAction;
    if (activeTab === "paid") return ["DepositPaid", "Paid", "NotRequired"].includes(a.paymentStatus);
    if (activeTab === "today") return isTodayInTimeZone(new Date(a.startAt), timeZone);
    return true;
  });

  const groups = filtered.reduce<Record<string, Appointment[]>>((acc, appt) => {
    if (!acc[appt.dayGroup]) acc[appt.dayGroup] = [];
    acc[appt.dayGroup].push(appt);
    return acc;
  }, {});

  const tabs: { key: FilterTab; label: string; count?: number }[] = [
    { key: "all", label: "All", count: appointments.length },
    { key: "needs-action", label: "Needs action", count: needsActionCount },
    { key: "paid", label: "Paid" },
    { key: "today", label: "Today" }
  ];

  return (
    <div className="flex min-h-0 flex-col border-r border-border bg-background">
      <div className="flex flex-shrink-0 gap-0.5 overflow-x-auto border-b border-border px-3 pt-3 pb-2">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            onClick={() => onTabChange(tab.key)}
            className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium whitespace-nowrap transition-colors ${
              activeTab === tab.key ? "bg-foreground text-background" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            {tab.label}
            {tab.count !== undefined && (
              <span
                className={`rounded-full px-1.5 text-[10px] tabular-nums ${
                  activeTab === tab.key ? "bg-background/20" : "bg-muted text-muted-foreground"
                }`}
              >
                {tab.count}
              </span>
            )}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-3 pb-6">
        {Object.entries(groups).map(([day, appts]) => (
          <div key={day}>
            <div className="px-1 pt-3 pb-1 text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
              {day}
            </div>
            {appts.map((appt) => (
              <button
                key={appt.id}
                type="button"
                onClick={() => onSelect(appt.id)}
                className={`relative mb-0.5 grid w-full grid-cols-[3.5rem_1fr_auto] gap-2.5 rounded-lg px-2.5 py-2.5 text-left transition-colors ${
                  selectedId === appt.id ? "border border-border bg-muted" : "border border-transparent hover:bg-muted"
                }`}
              >
                {appt.needsAction && <span className="absolute top-3.5 bottom-3.5 left-0 w-0.5 rounded bg-warning" />}
                <div className="font-mono text-[0.8125rem] leading-tight font-medium text-foreground">
                  {appt.time}
                  <div className="text-[10.5px] font-normal text-muted-foreground">{appt.duration}</div>
                </div>
                <div className="min-w-0">
                  <div className="truncate text-[0.8125rem] font-medium text-foreground">{appt.name}</div>
                  <div className="mt-0.5 flex flex-wrap items-center gap-2">
                    <span className={`inline-flex items-center gap-1 text-[11px] ${statusTextClass(appt.status)}`}>
                      <StatusDot status={appt.status} />
                      {appt.statusLabel}
                    </span>
                    <span className="text-[11px] text-muted-foreground">{appt.channel}</span>
                  </div>
                </div>
                <div className="self-center font-mono text-[0.8rem] font-medium whitespace-nowrap text-foreground">
                  {appt.amount}
                </div>
              </button>
            ))}
          </div>
        ))}
      </div>
    </div>
  );
}

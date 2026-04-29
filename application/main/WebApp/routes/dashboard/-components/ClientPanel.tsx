import { Trans } from "@lingui/react/macro";
import { useNavigate } from "@tanstack/react-router";

import { type Appointment } from "./appointmentTypes";

export function ClientPanel({ appointment }: { appointment: Appointment }) {
  const navigate = useNavigate();
  return (
    <div className="space-y-4 overflow-y-auto bg-background p-4">
      <div className="flex flex-col items-center rounded-xl border border-border p-3 text-center">
        <div className="mb-2 flex size-12 items-center justify-center rounded-full bg-foreground font-display text-lg font-semibold text-background">
          {appointment.name
            .split(" ")
            .slice(0, 2)
            .map((n) => n[0])
            .join("")}
        </div>
        <div className="text-sm font-semibold">{appointment.name}</div>
        <div className="mt-0.5 font-mono text-[11.5px] text-muted-foreground">+27 82 341 7890</div>
        <div className="mt-2 flex flex-wrap justify-center gap-1">
          <span className="rounded bg-muted px-1.5 py-0.5 text-[10.5px] font-medium text-foreground">Returning</span>
          <span className="rounded bg-muted px-1.5 py-0.5 text-[10.5px] font-medium text-foreground">VIP</span>
        </div>
      </div>

      <div className="grid grid-cols-3 gap-1.5">
        {[
          { value: "12", label: "Visits" },
          { value: "R 5 280", label: "Lifetime" },
          { value: "2%", label: "No-show" }
        ].map((stat) => (
          <div key={stat.label} className="rounded-lg border border-border p-2 text-center">
            <div className="font-display text-[15px] font-semibold">{stat.value}</div>
            <div className="mt-0.5 text-[10.5px] text-muted-foreground">{stat.label}</div>
          </div>
        ))}
      </div>

      <div>
        <div className="mb-1.5 text-[10.5px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
          <Trans>Alerts</Trans>
        </div>
        <div className="flex items-center gap-2 rounded-lg border border-warning/20 bg-warning/5 px-2.5 py-2 text-[12px] text-warning">
          <svg width="13" height="13" viewBox="0 0 13 13" fill="none" stroke="currentColor" strokeWidth="1.5">
            <path d="M6.5 1.5l5.5 9.5h-11z" />
            <line x1="6.5" y1="5" x2="6.5" y2="8" />
            <circle cx="6.5" cy="9.5" r="0.5" fill="currentColor" />
          </svg>
          Sensitive shoulder — prefers firm pressure
        </div>
      </div>

      <div>
        <div className="mb-1.5 text-[10.5px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
          <Trans>Recent visits</Trans>
        </div>
        {[
          { date: "12 Feb\n2026", service: "Full consultation", amount: "R 450 · paid" },
          { date: "04 Jan\n2026", service: "Follow-up visit", amount: "R 150 · paid" },
          { date: "18 Dec\n2025", service: "Express session", amount: "R 220 · paid" }
        ].map((visit, i) => (
          <div
            key={i}
            className="grid grid-cols-[3.5rem_1fr] gap-2 border-b border-border py-2 text-[12px] last:border-0"
          >
            <div className="font-mono text-[11px] leading-tight whitespace-pre-line text-muted-foreground">
              {visit.date}
            </div>
            <div>
              <div className="leading-tight text-foreground">{visit.service}</div>
              <div className="mt-0.5 text-[11px] text-muted-foreground">{visit.amount}</div>
            </div>
          </div>
        ))}
      </div>

      <div>
        <div className="mb-1.5 text-[10.5px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
          <Trans>Internal notes</Trans>
        </div>
        <div className="rounded-lg bg-muted p-2.5 text-[12px]">
          <div className="mb-0.5 text-[10.5px] text-muted-foreground">Sarah · 12 Feb</div>
          Switched to firm pressure on right side — much better feedback. Continue.
        </div>
      </div>

      <button
        type="button"
        onClick={() => navigate({ to: "/dashboard/clients" })}
        className="block w-full text-left text-[12px] font-medium text-foreground underline decoration-border underline-offset-3 transition-colors hover:decoration-foreground"
      >
        <Trans>View full profile →</Trans>
      </button>
    </div>
  );
}

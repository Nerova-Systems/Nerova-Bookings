import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import { StatusDot, type Appointment } from "./appointmentTypes";

export function AppointmentDetail({ appointment }: { appointment: Appointment }) {
  return (
    <div className="flex min-h-0 flex-col border-r border-border bg-background">
      <div className="flex-shrink-0 border-b border-border px-6 pt-4.5 pb-3.5">
        <div className="flex items-center gap-3">
          <h2 className="font-display text-[1.375rem]">{appointment.name}</h2>
          <span
            className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11.5px] font-medium ${
              appointment.status === "confirmed"
                ? "bg-success/10 text-success"
                : appointment.status === "pending"
                  ? "bg-warning/10 text-warning"
                  : "bg-destructive/10 text-destructive"
            }`}
          >
            <StatusDot status={appointment.status} />
            {appointment.statusLabel}
          </span>
        </div>
        <div className="mt-1 text-[12.5px] text-muted-foreground">
          Tue 22 April · 09:00 – 10:00 (60 min) · Sea Point studio
        </div>
      </div>

      <div className="flex-1 space-y-6 overflow-y-auto px-6 py-4">
        <div className="flex flex-wrap gap-2">
          <Button size="sm">
            <Trans>Confirm booking</Trans>
          </Button>
          <Button variant="outline" size="sm">
            <Trans>Send payment link</Trans>
          </Button>
          <Button variant="outline" size="sm">
            <Trans>Reschedule</Trans>
          </Button>
          <Button variant="outline" size="sm" className="px-2">
            <span className="flex gap-0.5">
              <span className="size-1 rounded-full bg-foreground" />
              <span className="size-1 rounded-full bg-foreground" />
              <span className="size-1 rounded-full bg-foreground" />
            </span>
          </Button>
        </div>

        <div>
          <div className="mb-2.5 text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <Trans>Booking</Trans>
          </div>
          <div className="grid grid-cols-[7rem_1fr] gap-x-3 gap-y-1.5 text-[0.8125rem]">
            <span className="text-muted-foreground">Service</span>
            <span className="text-foreground">
              <strong>{appointment.service}</strong>
              <span className="text-muted-foreground"> · 60 min · {appointment.amount}</span>
            </span>
            <span className="text-muted-foreground">When</span>
            <span>Tue 22 April · 09:00–10:00 · Africa/Johannesburg</span>
            <span className="text-muted-foreground">Where</span>
            <span>
              14 Main Rd, Sea Point, Cape Town{" "}
              <span className="ml-1 rounded bg-muted px-1.5 py-0.5 text-[10.5px] font-medium text-foreground">
                Physical
              </span>
            </span>
            <span className="text-muted-foreground">Booked</span>
            <span>22 Apr 06:14 via WhatsApp Flow</span>
            <span className="text-muted-foreground">Source</span>
            <span>+27 82 341 7890 · profile name "Liam B."</span>
          </div>
        </div>

        <div>
          <div className="mb-2.5 text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <Trans>Answers from the flow</Trans>
          </div>
          <div className="space-y-2.5">
            {[
              { q: "Is this your first visit?", a: "No — last visit was Feb 2026." },
              {
                q: "Anything we should know?",
                a: "Sore left shoulder from Tuesday's gym session. Prefer firm pressure on right side."
              }
            ].map((qa) => (
              <div key={qa.q} className="border-l-2 border-border pl-3">
                <div className="mb-0.5 text-[12px] text-muted-foreground">{qa.q}</div>
                <div className="text-[0.8125rem]">{qa.a}</div>
              </div>
            ))}
          </div>
        </div>

        <div>
          <div className="mb-2.5 flex items-center gap-2">
            <span className="text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
              <Trans>Conversation transcript</Trans>
            </span>
            <span className="inline-flex items-center gap-1 rounded-full bg-success/10 px-1.5 py-0.5 text-[10.5px] font-medium text-success">
              <svg width="9" height="9" viewBox="0 0 9 9" fill="none" stroke="currentColor" strokeWidth="1.6">
                <circle cx="4.5" cy="4.5" r="3.5" />
                <polyline points="3,4.5 4.2,5.7 6,3.7" />
              </svg>
              <Trans>Fully automated</Trans>
            </span>
          </div>
          <div className="flex flex-col gap-2 rounded-xl border border-border bg-muted p-3.5">
            <div className="flex max-w-[80%] flex-col gap-0.5 self-start">
              <span className="px-1 text-[10.5px] text-muted-foreground">Liam · 06:12</span>
              <div className="rounded-xl border border-border bg-background px-3 py-1.5 text-[0.8125rem] leading-snug">
                Hi! I&apos;d like to book.
              </div>
            </div>
            <div className="flex max-w-[80%] flex-col items-end gap-0.5 self-end">
              <span className="px-1 text-[10.5px] text-muted-foreground">Nerova bot · 06:12</span>
              <div className="rounded-xl border border-[rgba(44,122,79,0.2)] bg-[#dcf8c6] px-3 py-1.5 text-[0.8125rem] leading-snug text-[#1b3a26] dark:bg-[#1b3a26] dark:text-[#dcf8c6]">
                Hi Liam 👋 Tap below to pick a service &amp; time.
              </div>
              <div className="rounded-lg border border-border bg-background px-3 py-1.5 text-[0.8rem] font-medium text-foreground">
                📅 Book an appointment
              </div>
            </div>
            <div className="flex max-w-[80%] flex-col gap-0.5 self-start">
              <span className="px-1 text-[10.5px] text-muted-foreground">Liam · 06:14</span>
              <div className="rounded-xl border border-border bg-background px-3 py-1.5 text-[0.8125rem] leading-snug italic">
                Submitted booking flow: Full consultation · Tue 22 April · 09:00
              </div>
            </div>
            <div className="flex max-w-[80%] flex-col items-end gap-0.5 self-end">
              <span className="px-1 text-[10.5px] text-muted-foreground">Nerova bot · 06:14</span>
              <div className="rounded-xl border border-[rgba(44,122,79,0.2)] bg-[#dcf8c6] px-3 py-1.5 text-[0.8125rem] leading-snug text-[#1b3a26] dark:bg-[#1b3a26] dark:text-[#dcf8c6]">
                Thanks Liam — your request has been sent to Sarah&apos;s Studio for confirmation. We&apos;ll message you
                the moment it&apos;s confirmed.
              </div>
            </div>
          </div>
          <p className="mt-2 text-[11.5px] text-muted-foreground">
            <Trans>Conversations are managed by the Nerova bot. Handle the booking lifecycle from this panel.</Trans>
          </p>
        </div>
      </div>
    </div>
  );
}

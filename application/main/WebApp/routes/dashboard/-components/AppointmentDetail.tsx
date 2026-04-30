import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import { formatDayGroup, formatTime } from "@/shared/lib/dateFormatting";

import { AppointmentPaymentBlock } from "./AppointmentPaymentBlock";
import { StatusDot, type Appointment } from "./appointmentTypes";

interface AppointmentDetailProps {
  appointment: Appointment;
  onConfirm: (id: string) => void;
  onStatusChange: (id: string, status: string) => void;
  onCreateTerminalPayment: (id: string) => Promise<void>;
  isCreatingTerminalPayment: boolean;
}

export function AppointmentDetail({
  appointment,
  onConfirm,
  onStatusChange,
  onCreateTerminalPayment,
  isCreatingTerminalPayment
}: AppointmentDetailProps) {
  const when = formatDateTimeRange(appointment.startAt, appointment.endAt);
  const canConfirm = !["confirmed", "completed", "cancelled", "no-show"].includes(appointment.status);
  const runPrimaryAction = () => {
    if (appointment.status === "confirmed") {
      onStatusChange(appointment.id, "Completed");
      return;
    }
    onConfirm(appointment.id);
  };

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
          {when.summary} ({appointment.duration}) · {appointment.location}
        </div>
      </div>

      <div className="flex-1 space-y-5 overflow-y-auto px-6 py-4">
        <section className="rounded-xl border border-border bg-muted/40 p-3">
          <div className="mb-2 text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <Trans>Next action</Trans>
          </div>
          <div className="grid gap-3">
            <div className="flex flex-wrap items-center gap-2">
              <Button size="sm" onClick={runPrimaryAction} disabled={!canConfirm && appointment.status !== "confirmed"}>
                {appointment.status === "confirmed" ? <Trans>Complete appointment</Trans> : <Trans>Confirm booking</Trans>}
              </Button>
              <Button variant="outline" size="sm">
                <Trans>Reschedule</Trans>
              </Button>
            </div>
            <details className="group rounded-lg border border-border bg-background px-3 py-2">
              <summary className="cursor-pointer text-[12px] font-medium text-muted-foreground transition-colors group-open:text-foreground">
                <Trans>Close-out actions</Trans>
              </summary>
              <div className="mt-3 flex flex-wrap gap-2">
                <Button variant="outline" size="sm" onClick={() => onStatusChange(appointment.id, "Completed")}>
                  <Trans>Complete</Trans>
                </Button>
                <Button variant="outline" size="sm" onClick={() => onStatusChange(appointment.id, "NoShow")}>
                  <Trans>No-show</Trans>
                </Button>
                <Button variant="outline" size="sm" onClick={() => onStatusChange(appointment.id, "Cancelled")}>
                  <Trans>Cancel</Trans>
                </Button>
              </div>
            </details>
          </div>
        </section>

        <AppointmentPaymentBlock
          appointment={appointment}
          onCreateTerminalPayment={onCreateTerminalPayment}
          isCreatingTerminalPayment={isCreatingTerminalPayment}
        />

        <section>
          <div className="mb-2.5 text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <Trans>Booking</Trans>
          </div>
          <div className="grid grid-cols-[7rem_1fr] gap-x-3 gap-y-1.5 rounded-xl border border-border px-4 py-3 text-[0.8125rem]">
            <span className="text-muted-foreground">Service</span>
            <span className="text-foreground">
              <strong>{appointment.service}</strong>
              <span className="text-muted-foreground">
                {" "}
                · {appointment.duration} · {appointment.amount}
              </span>
            </span>
            <span className="text-muted-foreground">When</span>
            <span>{when.detail} · Africa/Johannesburg</span>
            <span className="text-muted-foreground">Where</span>
            <span>
              {appointment.location}{" "}
              <span className="ml-1 rounded bg-muted px-1.5 py-0.5 text-[10.5px] font-medium text-foreground">
                Physical
              </span>
            </span>
            <span className="text-muted-foreground">Booked via</span>
            <span>{appointment.channel.replace("via ", "")}</span>
            <span className="text-muted-foreground">Client</span>
            <span>
              {appointment.phone}
              {appointment.email ? ` · ${appointment.email}` : ""}
            </span>
          </div>
        </section>

        <details className="rounded-xl border border-border px-4 py-3">
          <summary className="cursor-pointer text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <Trans>Flow answers</Trans>
          </summary>
          <div className="mt-3 space-y-2.5">
            <AnswerLine label="Service request" value={`${appointment.service} for ${appointment.duration}.`} />
            <AnswerLine
              label="Operational note"
              value={appointment.clientAlert ?? appointment.clientInternalNote ?? "No intake notes captured yet."}
            />
          </div>
        </details>

        <details className="rounded-xl border border-border px-4 py-3">
          <summary className="cursor-pointer text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <Trans>Lifecycle events</Trans>
          </summary>
          <div className="mt-3 flex flex-col gap-2 rounded-xl bg-muted p-3.5">
            <EventLine title="Booking request" value={`${appointment.name} selected ${appointment.service}.`} />
            <EventLine title="Slot hold" value={`${when.summary} was reserved pending operator review.`} />
            <EventLine title="Payment state" value={appointment.statusLabel} />
          </div>
        </details>
      </div>
    </div>
  );
}

function AnswerLine({ label, value }: { label: string; value: string }) {
  return (
    <div className="border-l-2 border-border pl-3">
      <div className="mb-0.5 text-[12px] text-muted-foreground">{label}</div>
      <div className="text-[0.8125rem]">{value}</div>
    </div>
  );
}

function EventLine({ title, value }: { title: string; value: string }) {
  return (
    <div className="rounded-lg border border-border bg-background px-3 py-2">
      <div className="text-[10.5px] font-semibold tracking-[0.05em] text-muted-foreground uppercase">{title}</div>
      <div className="mt-0.5 text-[0.8125rem]">{value}</div>
    </div>
  );
}

function formatDateTimeRange(startAt: string, endAt: string) {
  const start = new Date(startAt);
  const end = new Date(endAt);
  const date = formatDayGroup(start);
  const startTime = formatTime(start);
  const endTime = formatTime(end);

  return {
    summary: `${date} · ${startTime}-${endTime}`,
    detail: `${date} · ${startTime}-${endTime}`
  };
}

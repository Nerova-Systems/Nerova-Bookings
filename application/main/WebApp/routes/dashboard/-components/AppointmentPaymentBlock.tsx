import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";

import { money, type Appointment } from "@/shared/lib/appointmentsApi";

export function AppointmentPaymentBlock({
  appointment,
  onCreateTerminalPayment,
  isCreatingTerminalPayment
}: {
  appointment: Appointment;
  onCreateTerminalPayment: (id: string) => Promise<void>;
  isCreatingTerminalPayment: boolean;
}) {
  const isPaid = ["Paid", "DepositPaid", "NotRequired"].includes(appointment.paymentStatus);
  const canUseTerminal = appointment.paymentPolicy === "CollectAfterAppointment" && !isPaid;
  return (
    <section className="rounded-xl border border-border px-4 py-3">
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <div className="text-[11px] font-semibold tracking-[0.06em] text-muted-foreground uppercase">
            <Trans>Payment</Trans>
          </div>
          <div className="mt-1 text-sm font-medium">{paymentPolicyText(appointment)}</div>
        </div>
        <span className={`rounded-full px-2 py-1 text-[11px] font-medium ${isPaid ? "bg-success/10 text-success" : "bg-warning/10 text-warning"}`}>
          {paymentStatusText(appointment.paymentStatus)}
        </span>
      </div>
      <div className="grid grid-cols-[7rem_1fr] gap-x-3 gap-y-1.5 text-[0.8125rem]">
        <span className="text-muted-foreground">Amount due</span>
        <span>{appointment.paymentAmountCents > 0 ? money(appointment.paymentAmountCents) : "None"}</span>
        <span className="text-muted-foreground">Provider</span>
        <span>{appointment.paymentPolicy === "NoPaymentRequired" ? "Not required" : "Paystack"}</span>
      </div>
      {canUseTerminal && (
        <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-border pt-3">
          <Button size="sm" onClick={() => onCreateTerminalPayment(appointment.id)} disabled={isCreatingTerminalPayment}>
            {isCreatingTerminalPayment ? <Trans>Opening terminal...</Trans> : <Trans>Collect with terminal</Trans>}
          </Button>
          <span className="text-xs text-muted-foreground">
            <Trans>Creates an appointment-linked Paystack terminal payment.</Trans>
          </span>
        </div>
      )}
      {!canUseTerminal && appointment.paymentPolicy !== "NoPaymentRequired" && !isPaid && (
        <div className="mt-3 rounded-lg bg-muted px-3 py-2 text-xs text-muted-foreground">
          <Trans>Waiting for Paystack verification. Payment state updates from verified callbacks or webhooks only.</Trans>
        </div>
      )}
    </section>
  );
}

function paymentPolicyText(appointment: Appointment) {
  if (appointment.paymentPolicy === "DepositBeforeBooking") return "Deposit before booking";
  if (appointment.paymentPolicy === "FullPaymentBeforeBooking") return "Full payment before booking";
  if (appointment.paymentPolicy === "CollectAfterAppointment") return "Collect after appointment";
  return "No payment required";
}

function paymentStatusText(paymentStatus: string) {
  if (paymentStatus === "DepositPaid") return "Deposit paid";
  if (paymentStatus === "Paid") return "Paid";
  if (paymentStatus === "NotRequired") return "Not required";
  if (paymentStatus === "Failed") return "Failed";
  return "Pending";
}

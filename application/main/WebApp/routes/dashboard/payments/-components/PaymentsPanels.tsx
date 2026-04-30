import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { CreditCardIcon, LandmarkIcon } from "lucide-react";

import { paymentMoney, type PaystackSubaccount, type PaymentIntent } from "@/shared/lib/paymentsApi";

export function PayoutPanel({ subaccount, onSetup }: { subaccount?: PaystackSubaccount; onSetup: () => void }) {
  const connected = Boolean(subaccount?.isActive);
  return (
    <section className="rounded-lg border border-border bg-background">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-border px-5 py-4">
        <div className="flex items-start gap-3">
          <div className="rounded-md border border-border bg-muted p-2">
            <LandmarkIcon className="size-4" />
          </div>
          <div>
            <h2 className="font-display text-base font-semibold">
              <Trans>Paystack payouts</Trans>
            </h2>
            <p className="mt-1 max-w-2xl text-sm text-muted-foreground">
              <Trans>Appointment payments settle into this business bank account through a Paystack subaccount.</Trans>
            </p>
          </div>
        </div>
        <span className={`rounded-full px-2.5 py-1 text-xs ${connected ? "bg-success/10 text-success" : "bg-warning/10 text-warning"}`}>
          {connected ? <Trans>Connected</Trans> : <Trans>Setup required</Trans>}
        </span>
      </div>
      <div className="grid gap-4 p-5 md:grid-cols-4">
        <Info label="Settlement bank" value={subaccount?.settlementBankName ?? "Not connected"} />
        <Info label="Account holder" value={subaccount?.accountName ?? "Not verified"} />
        <Info label="Account number" value={subaccount?.maskedAccountNumber ?? "Not saved"} />
        <Info label="Schedule" value={subaccount?.settlementSchedule ?? "Auto after setup"} />
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border px-5 py-3 text-sm">
        <span className="text-muted-foreground">
          {subaccount ? `Last synced ${formatDateTime(subaccount.lastSyncedAt)}` : "Connect payouts before accepting deposit payments."}
        </span>
        <Button variant="outline" size="sm" onClick={onSetup}>
          {subaccount ? <Trans>Change bank details</Trans> : <Trans>Set up Paystack payouts</Trans>}
        </Button>
      </div>
    </section>
  );
}

export function PaymentStatsPanel({ stats }: { stats?: PaymentStats }) {
  const items = [
    { label: "Tracked payments", value: String(stats?.totalTracked ?? 0) },
    { label: "Paid / confirmed", value: String(stats?.paidOrConfirmed ?? 0) },
    { label: "Needs action", value: String(stats?.needsAction ?? 0) },
    { label: "Pending value", value: paymentMoney(stats?.amountPendingCents ?? 0) },
    { label: "Paid value", value: paymentMoney(stats?.amountPaidCents ?? 0) },
    { label: "Overdue", value: String(stats?.overdue ?? 0) }
  ];
  return (
    <section className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      {items.map((item) => (
        <div key={item.label} className="rounded-lg border border-border bg-background px-4 py-3">
          <div className="text-xs text-muted-foreground">{item.label}</div>
          <div className="mt-1 font-display text-2xl font-semibold">{item.value}</div>
        </div>
      ))}
    </section>
  );
}

export function PaymentQueue({ payments }: { payments: PaymentIntent[] }) {
  return (
    <section className="overflow-hidden rounded-lg border border-border bg-background">
      <div className="flex items-center gap-2 border-b border-border px-4 py-3">
        <CreditCardIcon className="size-4 text-muted-foreground" />
        <h2 className="font-display text-sm font-semibold">
          <Trans>Recent appointment payments</Trans>
        </h2>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[42rem] text-left text-sm">
          <thead className="bg-muted text-xs text-muted-foreground">
            <tr>
              <th className="px-4 py-2 font-medium">Client</th>
              <th className="px-4 py-2 font-medium">Service</th>
              <th className="px-4 py-2 font-medium">Reference</th>
              <th className="px-4 py-2 font-medium">Amount</th>
              <th className="px-4 py-2 font-medium">State</th>
            </tr>
          </thead>
          <tbody>{payments.length === 0 ? <EmptyPaymentRow /> : payments.map((payment) => <PaymentRow key={payment.reference} payment={payment} />)}</tbody>
        </table>
      </div>
    </section>
  );
}

function PaymentRow({ payment }: { payment: PaymentIntent }) {
  return (
    <tr className="border-t border-border">
      <td className="px-4 py-3 font-medium">{payment.clientName}</td>
      <td className="px-4 py-3 text-muted-foreground">{payment.serviceName}</td>
      <td className="px-4 py-3 font-mono text-xs">{payment.reference}</td>
      <td className="px-4 py-3">{paymentMoney(payment.amountCents)}</td>
      <td className="px-4 py-3">
        <span className={`rounded-full px-2 py-1 text-xs ${payment.status === "Confirmed" ? "bg-success/10 text-success" : "bg-warning/10 text-warning"}`}>
          {payment.status}
        </span>
      </td>
    </tr>
  );
}

function EmptyPaymentRow() {
  return (
    <tr>
      <td colSpan={5} className="px-4 py-10 text-center text-muted-foreground">
        <Trans>No Paystack payment attempts yet.</Trans>
      </td>
    </tr>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="mt-1 truncate text-sm font-medium">{value}</div>
    </div>
  );
}

interface PaymentStats {
  totalTracked: number;
  paidOrConfirmed: number;
  needsAction: number;
  overdue: number;
  amountPendingCents: number;
  amountPaidCents: number;
}

const formatDateTime = (value: string) =>
  new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));

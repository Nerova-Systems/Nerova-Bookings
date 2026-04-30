import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute } from "@tanstack/react-router";
import { Loader2Icon } from "lucide-react";
import { useEffect, useState } from "react";

import { usePaymentOverview, usePaystackSettlements } from "@/shared/lib/paymentsApi";

import { PaymentQueue, PaymentStatsPanel, PayoutPanel } from "./-components/PaymentsPanels";
import { PaystackSetupDialog } from "./-components/PaystackSetupDialog";
import { SettlementPanel } from "./-components/SettlementPanel";

export const Route = createFileRoute("/dashboard/payments/")({
  staticData: { trackingTitle: "Payments" },
  component: PaymentsPage
});

function PaymentsPage() {
  const [setupOpen, setSetupOpen] = useState(false);
  const overviewQuery = usePaymentOverview();
  const subaccount = overviewQuery.data?.subaccount;
  const settlementsQuery = usePaystackSettlements(Boolean(subaccount?.isActive));

  useEffect(() => {
    document.title = t`Payments | Nerova`;
  }, []);

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
      <header className="sticky top-0 z-20 flex shrink-0 items-center justify-between gap-4 border-b border-border bg-background px-7 py-3.5">
        <div className="flex flex-col gap-0.5">
          <h1 className="font-display text-[1.375rem] leading-tight">
            <Trans>Payments</Trans>
          </h1>
          <span className="text-[12.5px] text-muted-foreground">
            <Trans>Appointment payments, Paystack payouts, and settlement status</Trans>
          </span>
        </div>
        <Button size="sm" onClick={() => setSetupOpen(true)}>
          {subaccount ? <Trans>Change bank details</Trans> : <Trans>Set up bank details</Trans>}
        </Button>
      </header>

      <main className="flex-1 overflow-y-auto px-7 py-6">
        {overviewQuery.isLoading ? (
          <div className="flex h-64 items-center justify-center text-sm text-muted-foreground">
            <Loader2Icon className="mr-2 size-4 animate-spin" />
            <Trans>Loading payments...</Trans>
          </div>
        ) : (
          <div className="grid gap-5">
            <PayoutPanel subaccount={subaccount} onSetup={() => setSetupOpen(true)} />
            <PaymentStatsPanel stats={overviewQuery.data?.stats} />
            <div className="grid gap-5 xl:grid-cols-[minmax(0,1.35fr)_minmax(22rem,0.65fr)]">
              <PaymentQueue payments={overviewQuery.data?.recentPayments ?? []} />
              <SettlementPanel
                loading={settlementsQuery.isLoading}
                settlements={settlementsQuery.data ?? []}
                hasSubaccount={Boolean(subaccount?.isActive)}
              />
            </div>
          </div>
        )}
      </main>

      {setupOpen && <PaystackSetupDialog subaccount={subaccount} onClose={() => setSetupOpen(false)} />}
    </div>
  );
}

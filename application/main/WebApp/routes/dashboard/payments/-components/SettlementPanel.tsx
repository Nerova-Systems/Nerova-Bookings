import { Trans } from "@lingui/react/macro";
import { RefreshCwIcon } from "lucide-react";

import { paymentMoney, type PaystackSettlement } from "@/shared/lib/paymentsApi";

export function SettlementPanel({
  loading,
  settlements,
  hasSubaccount
}: {
  loading: boolean;
  settlements: PaystackSettlement[];
  hasSubaccount: boolean;
}) {
  return (
    <section className="rounded-lg border border-border bg-background">
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <h2 className="font-display text-sm font-semibold">
          <Trans>Settlements</Trans>
        </h2>
        {loading && <RefreshCwIcon className="size-4 animate-spin text-muted-foreground" />}
      </div>
      <div className="divide-y divide-border">
        {!hasSubaccount && <EmptyLine text="Set up Paystack payouts to load settlement history." />}
        {hasSubaccount && !loading && settlements.length === 0 && <EmptyLine text="No settlements returned yet." />}
        {settlements.map((settlement) => (
          <div key={settlement.id} className="grid grid-cols-[1fr_auto] gap-3 px-4 py-3 text-sm">
            <div>
              <div className="font-medium">{settlement.status}</div>
              <div className="text-xs text-muted-foreground">
                {settlement.settlementDate ? formatDateTime(settlement.settlementDate) : "No date"}
              </div>
            </div>
            <div className="text-right">
              <div className="font-medium">{paymentMoney(settlement.effectiveAmountCents)}</div>
              <div className="text-xs text-muted-foreground">Fees {paymentMoney(settlement.feesCents)}</div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function EmptyLine({ text }: { text: string }) {
  return <div className="px-4 py-8 text-center text-sm text-muted-foreground">{text}</div>;
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}

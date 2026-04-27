import { Trans } from "@lingui/react/macro";

import type { components } from "@/shared/lib/api/api.generated";

type BillingInfo = components["schemas"]["BillingInfo"];

interface BillingInfoDisplayProps {
  billingInfo: BillingInfo | null | undefined;
}

export function BillingInfoDisplay({ billingInfo }: Readonly<BillingInfoDisplayProps>) {
  if (!billingInfo) {
    return (
      <div className="text-sm text-muted-foreground">
        <Trans>No billing information on file.</Trans>
      </div>
    );
  }

  const address = billingInfo.address;
  const cityLine = [address.postalCode, address.city, address.state].filter(Boolean).join(" ");

  return (
    <div className="text-sm">
      <div className="font-medium">{billingInfo.name}</div>
      <div className="text-muted-foreground">{address.line1}</div>
      {address.line2 && <div className="text-muted-foreground">{address.line2}</div>}
      <div className="text-muted-foreground">{cityLine}</div>
      <div className="text-muted-foreground">{address.country}</div>
      <div className="mt-1 text-muted-foreground">{billingInfo.email}</div>
      {billingInfo.taxId && (
        <div className="text-muted-foreground">
          <Trans>Tax ID:</Trans> {billingInfo.taxId}
        </div>
      )}
    </div>
  );
}

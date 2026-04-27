import { Trans } from "@lingui/react/macro";

import type { components } from "@/shared/lib/api/api.generated";

type PaymentMethod = components["schemas"]["PaymentMethod"];

interface PaymentMethodDisplayProps {
  paymentMethod: PaymentMethod | null | undefined;
}

export function PaymentMethodDisplay({ paymentMethod }: Readonly<PaymentMethodDisplayProps>) {
  if (!paymentMethod) {
    return (
      <div className="text-sm text-muted-foreground">
        <Trans>No payment method on file.</Trans>
      </div>
    );
  }

  // PayFast does not return card brand / last4 / expiry from the recurring API. The backend stores
  // a generic "Card on file" marker once a token is captured; if the card details are ever surfaced
  // by PayFast in future ITN payloads we can render them here.
  if (paymentMethod.last4 != null && paymentMethod.expMonth != null && paymentMethod.expYear != null) {
    return (
      <div className="text-sm">
        <div className="font-medium">
          {paymentMethod.brand} •••• {paymentMethod.last4}
        </div>
        <div className="text-muted-foreground">
          <Trans>
            Expires {paymentMethod.expMonth}/{paymentMethod.expYear}
          </Trans>
        </div>
      </div>
    );
  }

  return (
    <div className="text-sm">
      <div className="font-medium">
        <Trans>Card on file</Trans>
      </div>
      <div className="text-muted-foreground">
        <Trans>Managed by PayFast</Trans>
      </div>
    </div>
  );
}

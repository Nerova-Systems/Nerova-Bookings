import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { Separator } from "@repo/ui/components/Separator";
import { formatCurrency } from "@repo/utils/currency/formatCurrency";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import type { components } from "@/shared/lib/api/api.generated";

import { api } from "@/shared/lib/api/client";

import { BillingInfoDisplay } from "./BillingInfoDisplay";
import { PaymentMethodDisplay } from "./PaymentMethodDisplay";

type BillingInfo = components["schemas"]["BillingInfo"];
type PaymentMethod = components["schemas"]["PaymentMethod"];

interface RetryPaymentDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  billingInfo: BillingInfo | null | undefined;
  paymentMethod: PaymentMethod | null | undefined;
  amount: number;
  currency: string;
}

/**
 * Retry a failed billing-period charge against the saved PayFast token. The backend (POST
 * /api/account/billing/retry-pending-invoice) calls PayFast's /subscriptions/{token}/adhoc API.
 * If the token has been revoked / expired, the response includes a UUID for the lightbox so the
 * user can register a new card. Replaces upstream's Stripe invoice.payment_action_required flow.
 */
export function RetryPaymentDialog({
  isOpen,
  onOpenChange,
  billingInfo,
  paymentMethod,
  amount,
  currency
}: Readonly<RetryPaymentDialogProps>) {
  const queryClient = useQueryClient();

  const retryMutation = api.useMutation("post", "/api/account/billing/retry-pending-invoice", {
    onSuccess: (data) => {
      if (data.paid) {
        queryClient.invalidateQueries();
        toast.success(t`Pending payment completed`);
        onOpenChange(false);
        return;
      }

      if (data.uuid && typeof window.payfast_do_onsite_payment === "function") {
        window.payfast_do_onsite_payment({ uuid: data.uuid });
        onOpenChange(false);
        return;
      }

      toast.error(t`Payment could not be processed. Please update your card and try again.`);
    }
  });

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange} disablePointerDismissal={true} trackingTitle="Retry payment">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Retry pending payment</Trans>
          </DialogTitle>
        </DialogHeader>
        <DialogBody>
          <div className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <span className="text-sm font-medium">
                <Trans>Bill to</Trans>
              </span>
              <BillingInfoDisplay billingInfo={billingInfo} />
            </div>

            <Separator />

            <div className="flex flex-col gap-2">
              <span className="text-sm font-medium">
                <Trans>Payment method</Trans>
              </span>
              <PaymentMethodDisplay paymentMethod={paymentMethod} />
            </div>

            <Separator />

            <div className="flex items-baseline justify-between gap-4 font-medium">
              <span>
                <Trans>Total</Trans>
              </span>
              <span className="shrink-0 text-lg whitespace-nowrap tabular-nums">
                {formatCurrency(amount, currency)}
              </span>
            </div>
          </div>
        </DialogBody>
        <DialogFooter>
          <DialogClose render={<Button type="reset" variant="secondary" disabled={retryMutation.isPending} />}>
            <Trans>Cancel</Trans>
          </DialogClose>
          <Button onClick={() => retryMutation.mutate({})} isPending={retryMutation.isPending}>
            {retryMutation.isPending ? <Trans>Processing...</Trans> : <Trans>Pay</Trans>}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

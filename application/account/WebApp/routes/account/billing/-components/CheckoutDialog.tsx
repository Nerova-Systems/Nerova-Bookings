import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { LoaderCircleIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, type SubscriptionPlan as SubscriptionPlanType } from "@/shared/lib/api/client";

const ActivationPollingIntervalMs = 1000;
const ActivationTimeoutMs = 15_000;

interface CheckoutDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  plan: SubscriptionPlanType;
}

export function CheckoutDialog({ isOpen, onOpenChange, plan }: Readonly<CheckoutDialogProps>) {
  const [isLoading, setIsLoading] = useState(false);
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [isWaitingForActivation, setIsWaitingForActivation] = useState(false);

  const { data: subscription } = api.useQuery(
    "get",
    "/api/account/subscriptions/current",
    {},
    { refetchInterval: isWaitingForActivation ? ActivationPollingIntervalMs : false }
  );

  useEffect(() => {
    if (!isWaitingForActivation || !subscription?.hasPaystackSubscription) {
      return;
    }
    setIsWaitingForActivation(false);
    toast.success(t`Your subscription has been activated.`);
    onOpenChange(false);
  }, [isWaitingForActivation, subscription?.hasPaystackSubscription, onOpenChange]);

  useEffect(() => {
    if (!isWaitingForActivation) {
      return;
    }
    const timeout = setTimeout(() => {
      setIsWaitingForActivation(false);
      toast.warning(t`Your subscription may take a moment to appear.`);
      onOpenChange(false);
    }, ActivationTimeoutMs);
    return () => clearTimeout(timeout);
  }, [isWaitingForActivation, onOpenChange]);

  const checkoutMutation = api.useMutation("post", "/api/account/subscriptions/start-checkout", {
    onSuccess: (data) => {
      if (data.authorizationUrl) {
        window.location.assign(data.authorizationUrl);
        return;
      }

      setIsLoading(false);
      if (data.usedExistingPaymentMethod) {
        setIsWaitingForActivation(true);
        return;
      }

      setPaymentError(t`Payment checkout could not be started.`);
    },
    onError: () => {
      setIsLoading(false);
    }
  });
  const startCheckout = checkoutMutation.mutate;

  useEffect(() => {
    if (isOpen) {
      setPaymentError(null);
      setIsLoading(true);
      startCheckout({
        body: { plan }
      });
    } else {
      setIsLoading(false);
      setPaymentError(null);
      setIsWaitingForActivation(false);
    }
  }, [isOpen, plan, startCheckout]);

  return (
    <Dialog
      open={isOpen}
      onOpenChange={isWaitingForActivation || isLoading ? undefined : onOpenChange}
      disablePointerDismissal={true}
      trackingTitle="Checkout"
    >
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            {isWaitingForActivation ? <Trans>Activating subscription</Trans> : <Trans>Redirecting to checkout</Trans>}
          </DialogTitle>
          <DialogDescription>
            {isWaitingForActivation ? (
              <Trans>Please wait while we confirm your payment. This may take a few moments.</Trans>
            ) : (
              <Trans>You will be redirected to Paystack to complete payment.</Trans>
            )}
          </DialogDescription>
        </DialogHeader>
        <DialogBody>
          <div className="flex flex-col items-center gap-4 py-8">
            <LoaderCircleIcon className="size-8 animate-spin text-primary" />
            {paymentError ? (
              <p className="text-sm text-destructive">{paymentError}</p>
            ) : (
              <p className="text-sm text-muted-foreground">
                {isWaitingForActivation ? (
                  <Trans>Activating your subscription...</Trans>
                ) : (
                  <Trans>Preparing checkout...</Trans>
                )}
              </p>
            )}
          </div>
        </DialogBody>
        {paymentError && (
          <DialogFooter>
            <DialogClose render={<Button type="reset" variant="secondary" />}>
              <Trans>Close</Trans>
            </DialogClose>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  );
}

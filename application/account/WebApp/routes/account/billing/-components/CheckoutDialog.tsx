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
import { Skeleton } from "@repo/ui/components/Skeleton";
import { LoaderCircleIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, PaystackPaymentPurpose, type SubscriptionPlan as SubscriptionPlanType } from "@/shared/lib/api/client";

import { CheckoutForm, type PaystackCheckoutPayment } from "./CheckoutForm";

const ActivationPollingIntervalMs = 1000;
const ActivationTimeoutMs = 15_000;

interface CheckoutDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  plan: SubscriptionPlanType;
  prefetchedPayment?: PaystackCheckoutPayment;
}

export function CheckoutDialog({ isOpen, onOpenChange, plan, prefetchedPayment }: Readonly<CheckoutDialogProps>) {
  const [payment, setPayment] = useState<PaystackCheckoutPayment | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [isWaitingForActivation, setIsWaitingForActivation] = useState(false);

  const { data: subscription } = api.useQuery(
    "get",
    "/api/account/subscriptions/current",
    {},
    { refetchInterval: isWaitingForActivation ? ActivationPollingIntervalMs : false }
  );

  const confirmMutation = api.useMutation("post", "/api/account/subscriptions/confirm-payment", {
    onSuccess: () => {
      setIsLoading(false);
      setIsWaitingForActivation(true);
    },
    onError: () => {
      setIsLoading(false);
    }
  });

  const confirmPayment = (reference: string, purpose: PaystackPaymentPurpose) => {
    confirmMutation.mutate({
      body: {
        reference,
        plan,
        purpose
      }
    });
  };

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
      toast.success(t`Your subscription has been activated.`);
      onOpenChange(false);
    }, ActivationTimeoutMs);
    return () => clearTimeout(timeout);
  }, [isWaitingForActivation, onOpenChange]);

  const checkoutMutation = api.useMutation("post", "/api/account/subscriptions/start-checkout", {
    onSuccess: (data) => {
      const response = data as PaystackCheckoutPayment & { usedExistingPaymentMethod?: boolean };
      setPayment(response);
      if (response.usedExistingPaymentMethod && response.reference) {
        confirmPayment(response.reference, response.operationPurpose);
      } else {
        setIsLoading(false);
      }
    },
    onError: () => {
      setIsLoading(false);
    }
  });
  const startCheckout = checkoutMutation.mutate;

  useEffect(() => {
    if (isOpen) {
      setPaymentError(null);
      if (prefetchedPayment) {
        setPayment(prefetchedPayment);
        setIsLoading(false);
      } else {
        setIsLoading(true);
        setPayment(null);
        startCheckout({
          body: { plan }
        });
      }
    } else {
      setPayment(null);
      setIsLoading(false);
      setPaymentError(null);
      setIsWaitingForActivation(false);
    }
  }, [isOpen, prefetchedPayment, plan, startCheckout]);

  const handlePaymentCompleted = (reference: string) => {
    confirmPayment(reference, payment?.operationPurpose ?? PaystackPaymentPurpose.Subscribe);
  };

  const isReady = Boolean(payment?.accessCode);

  return (
    <Dialog
      open={isOpen}
      onOpenChange={isWaitingForActivation ? undefined : onOpenChange}
      disablePointerDismissal={true}
      trackingTitle="Checkout"
    >
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            {isWaitingForActivation ? <Trans>Activating subscription</Trans> : <Trans>Subscribe</Trans>}
          </DialogTitle>
          <DialogDescription>
            {isWaitingForActivation ? (
              <Trans>Please wait while we confirm your payment. This may take a few moments.</Trans>
            ) : (
              <Trans>Complete your payment to activate your subscription.</Trans>
            )}
          </DialogDescription>
        </DialogHeader>
        <DialogBody>
          {isWaitingForActivation ? (
            <div className="flex flex-col items-center gap-4 py-8">
              <LoaderCircleIcon className="size-8 animate-spin text-primary" />
              <p className="text-sm text-muted-foreground">
                <Trans>Activating your subscription...</Trans>
              </p>
            </div>
          ) : (
            <>
              {isLoading && <CheckoutSkeleton />}
              {paymentError && <div className="text-sm text-destructive">{paymentError}</div>}
              {isReady && payment && (
                <CheckoutForm
                  plan={plan}
                  payment={payment}
                  onPaymentCompleted={handlePaymentCompleted}
                  onError={setPaymentError}
                />
              )}
            </>
          )}
        </DialogBody>
        {!isReady && !isWaitingForActivation && (
          <DialogFooter>
            <DialogClose render={<Button type="reset" variant="secondary" />}>
              <Trans>Cancel</Trans>
            </DialogClose>
          </DialogFooter>
        )}
      </DialogContent>
    </Dialog>
  );
}

function CheckoutSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      <Skeleton className="h-[2.75rem] w-full" />
      <Skeleton className="h-[2.75rem] w-full" />
      <Skeleton className="h-[2.75rem] w-full" />
    </div>
  );
}

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
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import { resumePaystackTransaction } from "./paystackInline";

interface OpenInvoiceInfo {
  amount: number;
  currency: string;
}

interface UpdatePaymentMethodDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onHasOpenInvoice?: (invoice: OpenInvoiceInfo) => void;
}

type PaystackPaymentMethodSetup = {
  accessCode: string;
  reference: string;
};

export function UpdatePaymentMethodDialog({
  isOpen,
  onOpenChange,
  onHasOpenInvoice
}: Readonly<UpdatePaymentMethodDialogProps>) {
  const queryClient = useQueryClient();
  const [setup, setSetup] = useState<PaystackPaymentMethodSetup | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [isPaystackOpen, setIsPaystackOpen] = useState(false);
  const [setupError, setSetupError] = useState<string | null>(null);

  const confirmMutation = api.useMutation("post", "/api/account/billing/confirm-payment-method", {
    onSuccess: (data) => {
      queryClient.invalidateQueries();
      toast.success(t`Payment method updated`);
      const openInvoice =
        data.hasOpenInvoice && data.openInvoiceAmount != null && data.openInvoiceCurrency != null
          ? { amount: data.openInvoiceAmount, currency: data.openInvoiceCurrency }
          : null;
      onOpenChange(false);
      if (openInvoice) {
        onHasOpenInvoice?.(openInvoice);
      }
    }
  });

  const setupMutation = api.useMutation("post", "/api/account/billing/start-payment-method-setup", {
    onSuccess: (data) => {
      const response = data as PaystackPaymentMethodSetup;
      setSetup(response);
      setIsLoading(false);
    },
    onError: () => {
      setIsLoading(false);
    }
  });
  const startSetup = setupMutation.mutate;

  useEffect(() => {
    if (isOpen) {
      setIsLoading(true);
      setSetupError(null);
      setSetup(null);
      startSetup({});
    } else {
      setSetup(null);
      setSetupError(null);
      setIsLoading(false);
      setIsPaystackOpen(false);
    }
  }, [isOpen, startSetup]);

  const handleSubmit = async () => {
    if (!setup?.accessCode) {
      return;
    }

    setIsPaystackOpen(true);
    setSetupError(null);

    try {
      await resumePaystackTransaction(setup.accessCode, {
        onSuccess: (transaction) => {
          setIsPaystackOpen(false);
          confirmMutation.mutate({
            body: { reference: transaction.reference ?? transaction.trxref ?? setup.reference }
          });
        },
        onCancel: () => {
          setIsPaystackOpen(false);
        },
        onError: (error) => {
          setIsPaystackOpen(false);
          setSetupError(error.message ?? t`An error occurred while updating your payment method.`);
        }
      });
    } catch (error) {
      setIsPaystackOpen(false);
      setSetupError(error instanceof Error ? error.message : t`An error occurred while updating your payment method.`);
    }
  };

  const isPending = isPaystackOpen || confirmMutation.isPending;

  return (
    <Dialog
      open={isOpen}
      onOpenChange={onOpenChange}
      disablePointerDismissal={true}
      trackingTitle="Update payment method"
    >
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Update payment method</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Enter your new payment details below.</Trans>
          </DialogDescription>
        </DialogHeader>
        <DialogBody>
          {isLoading && <PaymentFormSkeleton />}
          {setupError && <div className="text-sm text-destructive">{setupError}</div>}
        </DialogBody>
        <DialogFooter>
          <DialogClose render={<Button type="reset" variant="secondary" disabled={isPending} />}>
            <Trans>Cancel</Trans>
          </DialogClose>
          <Button onClick={handleSubmit} isPending={isPending} disabled={!setup || isPending}>
            {isPending ? <Trans>Updating...</Trans> : <Trans>Update payment method</Trans>}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function PaymentFormSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      <Skeleton className="h-[2.75rem] w-full" />
      <Skeleton className="h-[2.75rem] w-full" />
      <Skeleton className="h-[2.75rem] w-full" />
    </div>
  );
}

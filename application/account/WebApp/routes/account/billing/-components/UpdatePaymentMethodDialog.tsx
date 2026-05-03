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

import { api } from "@/shared/lib/api/client";

interface UpdatePaymentMethodDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

export function UpdatePaymentMethodDialog({ isOpen, onOpenChange }: Readonly<UpdatePaymentMethodDialogProps>) {
  const [setupError, setSetupError] = useState<string | null>(null);

  const setupMutation = api.useMutation("post", "/api/account/billing/start-payment-method-setup", {
    onSuccess: (data) => {
      window.location.assign(data.updateUrl);
    },
    onError: () => {
      setSetupError(t`Payment method update could not be started.`);
    }
  });
  const startSetup = setupMutation.mutate;

  useEffect(() => {
    if (isOpen) {
      setSetupError(null);
      startSetup({});
    } else {
      setSetupError(null);
    }
  }, [isOpen, startSetup]);

  return (
    <Dialog
      open={isOpen}
      onOpenChange={setupMutation.isPending ? undefined : onOpenChange}
      disablePointerDismissal={true}
      trackingTitle="Update payment method"
    >
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Update payment method</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>You will be redirected to Paystack to update the payment method for this subscription.</Trans>
          </DialogDescription>
        </DialogHeader>
        <DialogBody>
          <div className="flex flex-col items-center gap-4 py-8">
            <LoaderCircleIcon className="size-8 animate-spin text-primary" />
            {setupError ? (
              <p className="text-sm text-destructive">{setupError}</p>
            ) : (
              <p className="text-sm text-muted-foreground">
                <Trans>Preparing payment method update...</Trans>
              </p>
            )}
          </div>
        </DialogBody>
        {setupError && (
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

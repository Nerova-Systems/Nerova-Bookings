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
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

interface UpdatePaymentMethodDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

/**
 * PayFast does not expose a card-collection iframe for updating an existing token's card. The
 * supported flow is to redirect the customer to PayFast's hosted page at
 * `/eng/recurring/update/{token}` where they enter new card details. The same token continues to
 * work after the update — no webhook fires for the card change itself, so the user simply returns
 * to our app and continues.
 */
export function UpdatePaymentMethodDialog({ isOpen, onOpenChange }: Readonly<UpdatePaymentMethodDialogProps>) {
  const queryClient = useQueryClient();

  const setupMutation = api.useMutation("post", "/api/account/billing/start-payment-method-setup", {
    onSuccess: (data) => {
      window.open(data.updateUrl, "_blank", "noopener,noreferrer");
      onOpenChange(false);
      toast.success(t`Opening PayFast in a new tab to update your card.`);
      queryClient.invalidateQueries({ queryKey: ["get", "/api/account/subscriptions/current"] });
    }
  });

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange} disablePointerDismissal={true} trackingTitle="Update payment method">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Update payment method</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>
              You'll be redirected to PayFast in a new tab where you can securely update the card on your subscription.
              Your existing schedule and billing dates remain unchanged.
            </Trans>
          </DialogDescription>
        </DialogHeader>
        <DialogBody>
          <div className="text-sm text-muted-foreground">
            <Trans>After updating your card, return to this tab — there's nothing else you need to do here.</Trans>
          </div>
        </DialogBody>
        <DialogFooter>
          <DialogClose render={<Button type="reset" variant="secondary" disabled={setupMutation.isPending} />}>
            <Trans>Cancel</Trans>
          </DialogClose>
          <Button onClick={() => setupMutation.mutate({})} isPending={setupMutation.isPending}>
            {setupMutation.isPending ? <Trans>Opening...</Trans> : <Trans>Update on PayFast</Trans>}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

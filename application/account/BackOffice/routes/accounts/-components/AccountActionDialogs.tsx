import { Trans } from "@lingui/react/macro";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogMedia,
  AlertDialogTitle
} from "@repo/ui/components/AlertDialog";
import { AlertTriangleIcon } from "lucide-react";

interface ConfirmDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onConfirm: () => void;
}

export function ReconcileConfirmDialog({ isOpen, onOpenChange, onConfirm }: Readonly<ConfirmDialogProps>) {
  return (
    <AlertDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Reconcile with Paystack confirm">
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogMedia className="bg-amber-100">
            <AlertTriangleIcon className="text-amber-600" />
          </AlertDialogMedia>
          <AlertDialogTitle>
            <Trans>Reconcile with Paystack?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>
              Reconcile verifies this tenant's pending Paystack payment attempts, processes missed webhook outcomes, and
              appends any missing billing events.
            </Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel variant="secondary">
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>
            <Trans>Reconcile</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

export function RefundInvoiceConfirmDialog({
  isOpen,
  isPending,
  onOpenChange,
  onConfirm,
  refundAmountLabel
}: Readonly<ConfirmDialogProps & { isPending: boolean; refundAmountLabel: string | null }>) {
  return (
    <AlertDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Refund invoice confirm">
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogMedia className="bg-destructive/10">
            <AlertTriangleIcon className="text-destructive" />
          </AlertDialogMedia>
          <AlertDialogTitle>
            <Trans>Refund latest invoice?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>
              This refunds the latest paid Paystack invoice{refundAmountLabel ? ` for ${refundAmountLabel}` : ""} and
              records a refund row. Subscription MRR stays unchanged.
            </Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel variant="secondary" disabled={isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={onConfirm} disabled={isPending}>
            {isPending ? <Trans>Refunding...</Trans> : <Trans>Refund</Trans>}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

export function CancelSubscriptionConfirmDialog({
  isOpen,
  isPending,
  onOpenChange,
  onConfirm
}: Readonly<ConfirmDialogProps & { isPending: boolean }>) {
  return (
    <AlertDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Cancel subscription confirm">
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogMedia className="bg-destructive/10">
            <AlertTriangleIcon className="text-destructive" />
          </AlertDialogMedia>
          <AlertDialogTitle>
            <Trans>Cancel subscription?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>
              This schedules the subscription to cancel at period end and stops future renewal charges. Current access
              remains active until the renewal date.
            </Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel variant="secondary" disabled={isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction variant="destructive" onClick={onConfirm} disabled={isPending}>
            {isPending ? <Trans>Canceling...</Trans> : <Trans>Cancel subscription</Trans>}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

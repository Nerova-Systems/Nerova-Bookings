import { t } from "@lingui/core/macro";
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
import { Button } from "@repo/ui/components/Button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { AlertTriangleIcon, MoreVerticalIcon, RotateCcwIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import type { components } from "@/shared/lib/api/client";

import { api, BackOfficeInvoiceRowKind, PaymentTransactionStatus } from "@/shared/lib/api/client";

type Invoice = components["schemas"]["BackOfficeInvoiceSummary"];

export function InvoiceActions({
  invoice,
  isAdmin,
  onRefunded
}: Readonly<{ invoice: Invoice; isAdmin: boolean; onRefunded: () => Promise<void> }>) {
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  const refundMutation = api.useMutation("post", "/api/back-office/invoices/{id}/refund", {
    onSuccess: async () => {
      toast.success(t`Invoice refunded`);
      setIsConfirmOpen(false);
      await onRefunded();
    }
  });

  const canRefund =
    isAdmin &&
    invoice.rowKind === BackOfficeInvoiceRowKind.Invoice &&
    invoice.status === PaymentTransactionStatus.Succeeded &&
    invoice.refundedAt == null &&
    invoice.creditNoteUrl == null;

  if (!canRefund) {
    return null;
  }

  const handleConfirmRefund = () => {
    refundMutation.mutate({ params: { path: { id: invoice.id } } });
  };

  const isWorking = refundMutation.isPending;

  return (
    <>
      <DropdownMenu trackingTitle="Invoice actions">
        <Tooltip>
          <TooltipTrigger
            render={
              <DropdownMenuTrigger
                render={
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={t`Invoice actions`}
                    disabled={isWorking}
                    onClick={(event) => event.stopPropagation()}
                  >
                    <MoreVerticalIcon className="size-4" />
                  </Button>
                }
              />
            }
          />
          <TooltipContent>{t`Invoice actions`}</TooltipContent>
        </Tooltip>
        <DropdownMenuContent align="end">
          <DropdownMenuItem
            trackingLabel="Refund invoice"
            variant="destructive"
            disabled={isWorking}
            onClick={() => setIsConfirmOpen(true)}
          >
            <RotateCcwIcon className="size-4" />
            {isWorking ? <Trans>Refunding...</Trans> : <Trans>Refund invoice</Trans>}
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <AlertDialog open={isConfirmOpen} onOpenChange={setIsConfirmOpen} trackingTitle="Refund invoice confirm">
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogMedia className="bg-destructive/10">
              <AlertTriangleIcon className="text-destructive" />
            </AlertDialogMedia>
            <AlertDialogTitle>
              <Trans>Refund invoice?</Trans>
            </AlertDialogTitle>
            <AlertDialogDescription>
              <Trans>
                This refunds the Paystack payment and records a refund row. Subscription MRR stays unchanged.
              </Trans>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel variant="secondary" disabled={isWorking}>
              <Trans>Cancel</Trans>
            </AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={handleConfirmRefund} disabled={isWorking}>
              {isWorking ? <Trans>Refunding...</Trans> : <Trans>Refund</Trans>}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}

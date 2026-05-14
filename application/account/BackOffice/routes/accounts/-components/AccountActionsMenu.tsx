import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { formatCurrency } from "@repo/utils/currency/formatCurrency";
import { ExternalLinkIcon, MoreVerticalIcon, RefreshCwIcon, RotateCcwIcon, XCircleIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { toast } from "sonner";

import type { components } from "@/shared/lib/api/client";

import { useMe } from "@/shared/hooks/useMe";
import {
  api,
  BackOfficeInvoiceRowKind,
  PaymentTransactionStatus,
  queryClient,
  SubscriptionPlan
} from "@/shared/lib/api/client";

import {
  CancelSubscriptionConfirmDialog,
  ReconcileConfirmDialog,
  RefundInvoiceConfirmDialog
} from "./AccountActionDialogs";
import { ReconcileResultDialog, type ReconcileResult } from "./ReconcileResultDialog";

type TenantDetailResponse = components["schemas"]["TenantDetailResponse"];

interface AccountActionsMenuProps {
  tenant: TenantDetailResponse | undefined;
  tenantId: string;
  stripeCustomerUrl: string | null | undefined;
}

export function AccountActionsMenu({ tenant, tenantId, stripeCustomerUrl }: Readonly<AccountActionsMenuProps>) {
  const { data: me } = useMe();
  const [result, setResult] = useState<ReconcileResult | null>(null);
  const [isResultOpen, setIsResultOpen] = useState(false);
  const [isReconcileConfirmOpen, setIsReconcileConfirmOpen] = useState(false);
  const [isRefundConfirmOpen, setIsRefundConfirmOpen] = useState(false);
  const [isCancelConfirmOpen, setIsCancelConfirmOpen] = useState(false);

  const paymentsQuery = api.useQuery(
    "get",
    "/api/back-office/tenants/{id}/payment-history",
    {
      params: {
        path: { id: tenantId },
        query: { PageSize: 100 }
      }
    },
    { enabled: me?.isAdmin === true }
  );

  const reconcileMutation = api.useMutation("post", "/api/back-office/tenants/{id}/reconcile-with-paystack", {
    onSuccess: (data) => {
      setResult(data);
      setIsResultOpen(true);
    }
  });

  const refundMutation = api.useMutation("post", "/api/back-office/invoices/{id}/refund", {
    onSuccess: async () => {
      toast.success(t`Invoice refunded`);
      setIsRefundConfirmOpen(false);
      await queryClient.invalidateQueries();
    }
  });

  const cancelMutation = api.useMutation("post", "/api/back-office/tenants/{id}/cancel-subscription", {
    onSuccess: async () => {
      toast.success(t`Subscription cancelled`);
      setIsCancelConfirmOpen(false);
      await queryClient.invalidateQueries();
    }
  });

  const refundableTransaction = useMemo(
    () =>
      paymentsQuery.data?.transactions.find(
        (transaction) =>
          transaction.rowKind === BackOfficeInvoiceRowKind.Invoice &&
          transaction.status === PaymentTransactionStatus.Succeeded &&
          transaction.refundedAt == null &&
          transaction.creditNoteUrl == null
      ),
    [paymentsQuery.data?.transactions]
  );

  // Reconcile with Paystack is admin-only on the server (TenantsEndpoints.cs). Hide the trigger for
  // non-admins so the UI matches the policy.
  if (!me?.isAdmin) {
    return null;
  }

  const handleConfirmReconcile = () => {
    setIsReconcileConfirmOpen(false);
    reconcileMutation.mutate({ params: { path: { id: tenantId } } });
  };

  const handleConfirmRefund = () => {
    if (!refundableTransaction) return;
    refundMutation.mutate({ params: { path: { id: refundableTransaction.id } } });
  };

  const handleConfirmCancel = () => {
    cancelMutation.mutate({ params: { path: { id: tenantId } } });
  };

  const canRefund = refundableTransaction != null;
  const canCancel = tenant != null && tenant.plan !== SubscriptionPlan.Basis && !tenant.cancelAtPeriodEnd;
  const isWorking = reconcileMutation.isPending || refundMutation.isPending || cancelMutation.isPending;
  const refundAmountLabel = refundableTransaction
    ? formatCurrency(refundableTransaction.amount, refundableTransaction.currency)
    : null;

  return (
    <>
      <DropdownMenu trackingTitle="Account actions">
        <Tooltip>
          <TooltipTrigger
            render={
              <DropdownMenuTrigger
                render={
                  <Button variant="outline" size="icon-sm" aria-label={t`Account actions`} disabled={isWorking}>
                    <MoreVerticalIcon className="size-4" />
                  </Button>
                }
              />
            }
          />
          <TooltipContent>{t`Account actions`}</TooltipContent>
        </Tooltip>
        <DropdownMenuContent align="end">
          <DropdownMenuItem
            trackingLabel="Reconcile with Paystack"
            onClick={() => setIsReconcileConfirmOpen(true)}
            disabled={isWorking}
          >
            <RefreshCwIcon className="size-4" />
            {reconcileMutation.isPending ? <Trans>Reconciling...</Trans> : <Trans>Reconcile with Paystack</Trans>}
          </DropdownMenuItem>
          <DropdownMenuItem
            trackingLabel="Refund latest invoice"
            variant="destructive"
            onClick={() => setIsRefundConfirmOpen(true)}
            disabled={isWorking || paymentsQuery.isLoading || !canRefund}
          >
            <RotateCcwIcon className="size-4" />
            {refundMutation.isPending ? <Trans>Refunding...</Trans> : <Trans>Refund latest invoice</Trans>}
          </DropdownMenuItem>
          <DropdownMenuItem
            trackingLabel="Cancel subscription"
            variant="destructive"
            onClick={() => setIsCancelConfirmOpen(true)}
            disabled={isWorking || !canCancel}
          >
            <XCircleIcon className="size-4" />
            {cancelMutation.isPending ? <Trans>Canceling...</Trans> : <Trans>Cancel subscription</Trans>}
          </DropdownMenuItem>
          {stripeCustomerUrl && (
            <DropdownMenuItem
              trackingLabel="Open in Stripe"
              onClick={() => window.open(stripeCustomerUrl, "_blank", "noopener,noreferrer")}
            >
              <ExternalLinkIcon className="size-4" />
              <Trans>Open in Stripe</Trans>
            </DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      <ReconcileConfirmDialog
        isOpen={isReconcileConfirmOpen}
        onOpenChange={setIsReconcileConfirmOpen}
        onConfirm={handleConfirmReconcile}
      />
      <RefundInvoiceConfirmDialog
        isOpen={isRefundConfirmOpen}
        isPending={refundMutation.isPending}
        onOpenChange={setIsRefundConfirmOpen}
        onConfirm={handleConfirmRefund}
        refundAmountLabel={refundAmountLabel}
      />
      <CancelSubscriptionConfirmDialog
        isOpen={isCancelConfirmOpen}
        isPending={cancelMutation.isPending}
        onOpenChange={setIsCancelConfirmOpen}
        onConfirm={handleConfirmCancel}
      />
      <ReconcileResultDialog isOpen={isResultOpen} onOpenChange={setIsResultOpen} result={result} />
    </>
  );
}

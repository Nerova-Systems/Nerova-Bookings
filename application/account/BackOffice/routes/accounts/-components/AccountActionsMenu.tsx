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
import { AlertTriangleIcon, ExternalLinkIcon, MoreVerticalIcon, RefreshCwIcon } from "lucide-react";
import { useState } from "react";

import { useMe } from "@/shared/hooks/useMe";
import { api } from "@/shared/lib/api/client";

import { ReconcileResultDialog, type ReconcileResult } from "./ReconcileResultDialog";

interface AccountActionsMenuProps {
  tenantId: string;
  stripeCustomerUrl: string | null | undefined;
}

export function AccountActionsMenu({ tenantId, stripeCustomerUrl }: Readonly<AccountActionsMenuProps>) {
  const { data: me } = useMe();
  const [result, setResult] = useState<ReconcileResult | null>(null);
  const [isResultOpen, setIsResultOpen] = useState(false);
  const [isConfirmOpen, setIsConfirmOpen] = useState(false);

  const reconcileMutation = api.useMutation("post", "/api/back-office/tenants/{id}/reconcile-with-paystack", {
    onSuccess: (data) => {
      setResult(data);
      setIsResultOpen(true);
    }
  });

  // Reconcile with Paystack is admin-only on the server (TenantsEndpoints.cs). Hide the trigger for
  // non-admins so the UI matches the policy.
  if (!me?.isAdmin) {
    return null;
  }

  const handleConfirmReconcile = () => {
    setIsConfirmOpen(false);
    reconcileMutation.mutate({ params: { path: { id: tenantId } } });
  };

  const isWorking = reconcileMutation.isPending;

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
            onClick={() => setIsConfirmOpen(true)}
            disabled={isWorking}
          >
            <RefreshCwIcon className="size-4" />
            {reconcileMutation.isPending ? <Trans>Reconciling...</Trans> : <Trans>Reconcile with Paystack</Trans>}
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

      <AlertDialog open={isConfirmOpen} onOpenChange={setIsConfirmOpen} trackingTitle="Reconcile with Paystack confirm">
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
                Reconcile verifies this tenant's pending Paystack payment attempts, processes missed webhook outcomes,
                and appends any missing billing events.
              </Trans>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel variant="secondary">
              <Trans>Cancel</Trans>
            </AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmReconcile}>
              <Trans>Reconcile</Trans>
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <ReconcileResultDialog isOpen={isResultOpen} onOpenChange={setIsResultOpen} result={result} />
    </>
  );
}

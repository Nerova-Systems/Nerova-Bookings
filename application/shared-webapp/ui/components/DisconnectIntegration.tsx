import { Trans, useLingui } from "@lingui/react/macro";
import { UnplugIcon } from "lucide-react";
import { useState } from "react";

import { Button } from "./Button";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "./Dialog";

/**
 * Disconnect integration button + confirmation dialog.
 * Ported from cal.com `packages/ui/components/apps/DisconnectIntegration.tsx` (cf2a55c).
 *
 * No prop deviations.
 */
interface DisconnectIntegrationProps {
  /** Integration / credential ID to disconnect. */
  credentialId: number;
  /** Display name of the integration (shown in the confirmation dialog). */
  label?: string;
  /** Called when the user confirms disconnection. */
  onDisconnect?: (credentialId: number) => void | Promise<void>;
  /** Whether the disconnect operation is currently in progress. */
  isLoading?: boolean;
  /** Custom trigger button label. */
  buttonText?: React.ReactNode;
  /** Custom variant for the trigger button. @default "destructive" */
  buttonVariant?: "default" | "destructive" | "outline" | "ghost";
  className?: string;
}

export function DisconnectIntegration({
  credentialId,
  label,
  onDisconnect,
  isLoading,
  buttonText,
  buttonVariant = "destructive",
  className
}: DisconnectIntegrationProps) {
  const { t } = useLingui();
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleConfirm = async () => {
    setLoading(true);
    try {
      await onDisconnect?.(credentialId);
    } finally {
      setLoading(false);
      setOpen(false);
    }
  };

  const isProcessing = isLoading ?? loading;

  return (
    <>
      <Button
        variant={buttonVariant}
        size="sm"
        onClick={() => setOpen(true)}
        disabled={isProcessing}
        className={className}
        aria-label={label ? t`Disconnect ${label}` : t`Disconnect integration`}
      >
        <UnplugIcon className="size-4" />
        {buttonText ?? <Trans>Disconnect</Trans>}
      </Button>

      <Dialog trackingTitle="Disconnect Integration" open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              <Trans>Disconnect integration</Trans>
            </DialogTitle>
            <DialogDescription>
              {label ? (
                <Trans>
                  Are you sure you want to disconnect <strong>{label}</strong>? This cannot be undone.
                </Trans>
              ) : (
                <Trans>Are you sure you want to disconnect this integration? This cannot be undone.</Trans>
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)} disabled={isProcessing}>
              <Trans>Cancel</Trans>
            </Button>
            <Button variant="destructive" onClick={handleConfirm} disabled={isProcessing}>
              {isProcessing ? <Trans>Disconnecting…</Trans> : <Trans>Disconnect</Trans>}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

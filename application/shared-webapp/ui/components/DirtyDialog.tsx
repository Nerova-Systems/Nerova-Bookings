import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { type ReactNode, useCallback, useEffect, useMemo, useState } from "react";

import { useUnsavedChangesGuard } from "../hooks/useUnsavedChangesGuard";
import { Dialog, type DialogProps } from "./Dialog";
import { DirtyDialogContext, type DirtyDialogContextValue } from "./DirtyDialogContext";
import { UnsavedChangesAlertDialog } from "./UnsavedChangesAlertDialog";

export { DirtyDialogContext };

export type DirtyDialogProps = Omit<DialogProps, "onOpenChange"> & {
  onOpenChange: (isOpen: boolean) => void;
  unsavedChangesTitle?: string;
  unsavedChangesMessage?: ReactNode;
  leaveLabel?: string;
  stayLabel?: string;
};

/**
 * A Dialog wrapper that warns users about unsaved changes before closing.
 *
 * The dialog body (rendered as a child component inside <DialogContent>) declares its own state
 * and signals dirtiness via the `useDialogSetDirty()` hook. The body unmounts on close, so
 * reopening starts fresh — no manual reset is required.
 */
export function DirtyDialog({
  open,
  onOpenChange,
  unsavedChangesTitle = t`Unsaved changes`,
  unsavedChangesMessage = <Trans>You have unsaved changes. If you leave now, your changes will be lost.</Trans>,
  leaveLabel = t`Leave`,
  stayLabel = t`Stay`,
  children,
  ...dialogProps
}: Readonly<DirtyDialogProps>) {
  const [isDirty, setIsDirty] = useState(false);

  const { isConfirmDialogOpen, confirmLeave, cancelLeave, guardedOnOpenChange } = useUnsavedChangesGuard({
    hasUnsavedChanges: isDirty
  });

  const closeDialog = useCallback(() => {
    onOpenChange(false);
  }, [onOpenChange]);

  const handleDialogOpenChange = useCallback(
    (newOpen: boolean) => {
      if (newOpen) {
        onOpenChange(newOpen);
      } else {
        guardedOnOpenChange(newOpen, closeDialog);
      }
    },
    [guardedOnOpenChange, closeDialog, onOpenChange]
  );

  // When the dialog closes (X, Esc, Cancel, programmatic close after submit, etc.) wipe the dirty
  // flag so the next open starts clean. The body unmounts anyway, so its own state is gone; this
  // just keeps the DirtyDialog's own state in sync.
  useEffect(() => {
    if (!open) {
      setIsDirty(false);
    }
  }, [open]);

  const contextValue = useMemo<DirtyDialogContextValue>(
    () => ({ setDirty: setIsDirty, cancel: closeDialog, hasUnsavedChanges: isDirty }),
    [closeDialog, isDirty]
  );

  return (
    <>
      <DirtyDialogContext.Provider value={contextValue}>
        <Dialog open={open} onOpenChange={handleDialogOpenChange} {...dialogProps}>
          {children}
        </Dialog>
      </DirtyDialogContext.Provider>

      <UnsavedChangesAlertDialog
        isOpen={isConfirmDialogOpen}
        onConfirmLeave={confirmLeave}
        onCancel={cancelLeave}
        title={unsavedChangesTitle}
        actionLabel={leaveLabel}
        cancelLabel={stayLabel}
        parentTrackingTitle={dialogProps.trackingTitle}
      >
        {unsavedChangesMessage}
      </UnsavedChangesAlertDialog>
    </>
  );
}

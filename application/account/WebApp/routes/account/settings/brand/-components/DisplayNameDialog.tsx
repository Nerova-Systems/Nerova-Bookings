import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import {
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { TextField } from "@repo/ui/components/TextField";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { InfoIcon } from "lucide-react";
import { toast } from "sonner";

import { api, WabaDisplayNameStatus, type Schemas } from "@/shared/lib/api/client";

const DISPLAY_NAME_MAX_LENGTH = 75;
// Allowed: letters, digits, whitespace, ' . , & - ( ) /
const DISPLAY_NAME_PATTERN = /^[a-zA-Z0-9\s'.,&\-()/]+$/;

interface DisplayNameDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  currentStatus: Schemas["WabaDisplayNameStatusResponse"] | null | undefined;
}

export function DisplayNameDialog({ isOpen, onOpenChange, currentStatus }: Readonly<DisplayNameDialogProps>) {
  const handleClose = () => onOpenChange(false);

  return (
    <DirtyDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Request display name change">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Request display name change</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>
              Meta reviews display name changes. Approval typically takes 1–3 business days. Only one pending request is
              allowed at a time.
            </Trans>
          </DialogDescription>
        </DialogHeader>
        <DisplayNameDialogBody currentStatus={currentStatus} onClose={handleClose} />
      </DialogContent>
    </DirtyDialog>
  );
}

function DisplayNameDialogBody({
  currentStatus,
  onClose
}: {
  currentStatus: Schemas["WabaDisplayNameStatusResponse"] | null | undefined;
  onClose: () => void;
}) {
  const setDirty = useDialogSetDirty();

  const requestDisplayNameMutation = api.useMutation("post", "/api/whatsapp/display-name", {
    onSuccess: () => {
      toast.success(t`Display name change request submitted`);
      onClose();
    }
  });

  const isPendingReview = currentStatus?.status === WabaDisplayNameStatus.PendingReview;

  return (
    <DialogForm
      onSubmit={mutationSubmitter(requestDisplayNameMutation)}
      validationErrors={requestDisplayNameMutation.error?.errors}
    >
      <DialogBody className="flex flex-col gap-4">
        {isPendingReview && (
          <div className="flex items-start gap-2 rounded-md bg-warning/10 px-3 py-2.5 text-sm text-warning">
            <InfoIcon className="mt-0.5 size-4 shrink-0" />
            <span>
              <Trans>
                A name change request is already pending review:{" "}
                <strong>&ldquo;{currentStatus?.requestedDisplayName}&rdquo;</strong>. Submitting a new request will
                replace it.
              </Trans>
            </span>
          </div>
        )}

        <TextField
          autoFocus={true}
          required={true}
          name="requestedDisplayName"
          label={t`Requested display name`}
          maxLength={DISPLAY_NAME_MAX_LENGTH}
          description={t`Max 75 characters. Allowed: letters, digits, spaces, and ' . , & - ( ) /`}
          pattern={DISPLAY_NAME_PATTERN.source}
          onChange={() => setDirty(true)}
        />

        <div className="flex items-start gap-2 rounded-md bg-info/10 px-3 py-2.5 text-sm text-info">
          <InfoIcon className="mt-0.5 size-4 shrink-0" />
          <span>
            <Trans>
              After approval, your WhatsApp display name will update automatically. Meta&apos;s review typically takes
              1–3 business days.
            </Trans>
          </span>
        </div>
      </DialogBody>

      <DialogFooter>
        <DialogClose
          render={<Button type="reset" variant="secondary" disabled={requestDisplayNameMutation.isPending} />}
        >
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={requestDisplayNameMutation.isPending}>
          {requestDisplayNameMutation.isPending ? <Trans>Submitting...</Trans> : <Trans>Submit request</Trans>}
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

export function DisplayNameStatusBadge({
  status
}: Readonly<{ status: Schemas["WabaDisplayNameStatusResponse"] | null | undefined }>) {
  if (!status || status.status === WabaDisplayNameStatus.None) {
    return (
      <Badge variant="outline">
        <Trans>Not set</Trans>
      </Badge>
    );
  }

  switch (status.status) {
    case WabaDisplayNameStatus.Approved:
      return (
        <Badge variant="default">
          <Trans>Approved</Trans>
        </Badge>
      );
    case WabaDisplayNameStatus.PendingReview:
      return (
        <Badge variant="warning">
          <Trans>Pending review</Trans>
        </Badge>
      );
    case WabaDisplayNameStatus.Declined:
      return (
        <Badge variant="destructive">
          <Trans>Declined</Trans>
        </Badge>
      );
    case WabaDisplayNameStatus.Expired:
      return (
        <Badge variant="outline">
          <Trans>Expired</Trans>
        </Badge>
      );
    default:
      return null;
  }
}

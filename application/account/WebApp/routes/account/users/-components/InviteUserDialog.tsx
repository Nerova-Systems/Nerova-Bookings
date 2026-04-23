import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { Form } from "@repo/ui/components/Form";
import { TextField } from "@repo/ui/components/TextField";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

interface InviteUserDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

export default function InviteUserDialog({ isOpen, onOpenChange }: Readonly<InviteUserDialogProps>) {
  const handleClose = () => onOpenChange(false);

  return (
    <DirtyDialog open={isOpen} onOpenChange={onOpenChange} trackingTitle="Invite user">
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Invite user</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>An email with login instructions will be sent to the user.</Trans>
          </DialogDescription>
        </DialogHeader>
        <InviteUserDialogBody onClose={handleClose} />
      </DialogContent>
    </DirtyDialog>
  );
}

function InviteUserDialogBody({ onClose }: { onClose: () => void }) {
  const setDirty = useDialogSetDirty();
  const inviteUserMutation = api.useMutation("post", "/api/account/users/invite", {
    onSuccess: () => {
      toast.success(t`User invited successfully`);
      onClose();
    }
  });

  return (
    <Form
      onSubmit={mutationSubmitter(inviteUserMutation)}
      validationErrors={inviteUserMutation.error?.errors}
      validationBehavior="aria"
      className="flex flex-col max-sm:h-full"
    >
      <DialogBody>
        <TextField
          autoFocus={true}
          required={true}
          name="email"
          label={t`Email`}
          placeholder={t`user@email.com`}
          className="flex-grow"
          onChange={() => setDirty(true)}
        />
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={inviteUserMutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" disabled={inviteUserMutation.isPending}>
          {inviteUserMutation.isPending ? <Trans>Sending...</Trans> : <Trans>Send invite</Trans>}
        </Button>
      </DialogFooter>
    </Form>
  );
}

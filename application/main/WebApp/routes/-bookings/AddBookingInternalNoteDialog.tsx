import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { Label } from "@repo/ui/components/Label";
import { Textarea } from "@repo/ui/components/Textarea";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";

export function AddBookingInternalNoteDialog({
  booking,
  isOpen,
  onOpenChange,
  onAdded
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onAdded?: () => void;
}>) {
  return (
    <Dialog trackingTitle={t`Add internal note`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Add internal note</Trans>
          </DialogTitle>
        </DialogHeader>
        {booking && (
          <AddBookingInternalNoteDialogBody booking={booking} onClose={() => onOpenChange(false)} onAdded={onAdded} />
        )}
      </DialogContent>
    </Dialog>
  );
}

function AddBookingInternalNoteDialogBody({
  booking,
  onClose,
  onAdded
}: Readonly<{ booking: BookingListItem; onClose: () => void; onAdded?: () => void }>) {
  const [body, setBody] = useState("");
  const mutation = api.useMutation("post", "/api/bookings/{id}/notes", {
    onSuccess: () => {
      toast.success(t`Note added`);
      void queryClient.invalidateQueries();
      onClose();
      onAdded?.();
    }
  });

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() => {
        if (body.trim().length === 0) return;
        mutation.mutate({
          params: { path: { id: booking.id } },
          body: { id: booking.id, body: body.trim() }
        });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <div className="flex flex-col gap-2">
          <Label htmlFor="internal-note">
            <Trans>Note</Trans>
          </Label>
          <Textarea
            id="internal-note"
            name="body"
            required={true}
            autoFocus={true}
            value={body}
            placeholder={t`Notes are only visible to your team`}
            onChange={(event) => setBody(event.currentTarget.value)}
          />
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending} disabled={body.trim().length === 0}>
          <Trans>Add note</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

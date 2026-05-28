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
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { Input } from "@repo/ui/components/Input";
import { Label } from "@repo/ui/components/Label";
import { Textarea } from "@repo/ui/components/Textarea";
import { useState } from "react";
import { toast } from "sonner";

import type { Schemas } from "@/shared/lib/api/client";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";

type UserId = Schemas["UserId"];

// TODO: Wave 4.5 — replace plain text input with a host picker (search users in the booker's team).
export function ReassignBookingDialog({
  booking,
  isOpen,
  onOpenChange,
  onReassigned
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onReassigned?: () => void;
}>) {
  return (
    <Dialog trackingTitle={t`Reassign host`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Reassign host</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Move this booking to a different host. The new host will be notified.</Trans>
          </DialogDescription>
        </DialogHeader>
        {booking && (
          <ReassignBookingDialogBody
            booking={booking}
            onClose={() => onOpenChange(false)}
            onReassigned={onReassigned}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function ReassignBookingDialogBody({
  booking,
  onClose,
  onReassigned
}: Readonly<{ booking: BookingListItem; onClose: () => void; onReassigned?: () => void }>) {
  const [newOwnerUserId, setNewOwnerUserId] = useState("");
  const [reason, setReason] = useState("");
  const mutation = api.useMutation("post", "/api/bookings/{id}/reassign", {
    onSuccess: () => {
      toast.success(t`Booking reassigned`);
      void queryClient.invalidateQueries();
      onClose();
      onReassigned?.();
    }
  });

  const trimmedId = newOwnerUserId.trim();

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() => {
        if (trimmedId.length === 0) return;
        mutation.mutate({
          params: { path: { id: booking.id } },
          body: {
            id: booking.id,
            newOwnerUserId: trimmedId as UserId,
            reason: reason.trim() || null
          }
        });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <div className="flex flex-col gap-2">
          <Label htmlFor="reassign-host-id">
            <Trans>New host user ID</Trans>
          </Label>
          <Input
            id="reassign-host-id"
            name="newOwnerUserId"
            autoFocus={true}
            required={true}
            value={newOwnerUserId}
            placeholder={t`user_...`}
            onChange={(event) => setNewOwnerUserId(event.currentTarget.value)}
          />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="reassign-reason">
            <Trans>Reason (optional)</Trans>
          </Label>
          <Textarea
            id="reassign-reason"
            name="reason"
            value={reason}
            placeholder={t`Why is this booking being reassigned?`}
            onChange={(event) => setReason(event.currentTarget.value)}
          />
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending} disabled={trimmedId.length === 0}>
          <Trans>Reassign</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

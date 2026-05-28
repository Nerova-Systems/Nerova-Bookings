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
import { Label } from "@repo/ui/components/Label";
import { Textarea } from "@repo/ui/components/Textarea";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";

export function RequestRescheduleDialog({
  booking,
  isOpen,
  onOpenChange,
  onRequested
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onRequested?: () => void;
}>) {
  return (
    <Dialog trackingTitle={t`Request reschedule`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Request reschedule</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>The attendee will be asked to pick a new time. Add an optional message.</Trans>
          </DialogDescription>
        </DialogHeader>
        {booking && (
          <RequestRescheduleDialogBody
            booking={booking}
            onClose={() => onOpenChange(false)}
            onRequested={onRequested}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function RequestRescheduleDialogBody({
  booking,
  onClose,
  onRequested
}: Readonly<{ booking: BookingListItem; onClose: () => void; onRequested?: () => void }>) {
  const [reason, setReason] = useState("");
  const mutation = api.useMutation("post", "/api/bookings/{id}/request-reschedule", {
    onSuccess: () => {
      toast.success(t`Reschedule requested`);
      void queryClient.invalidateQueries();
      onClose();
      onRequested?.();
    }
  });

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() => {
        mutation.mutate({
          params: { path: { id: booking.id } },
          body: { rescheduleReason: reason.trim() || null }
        });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <div className="flex flex-col gap-2">
          <Label htmlFor="reschedule-reason">
            <Trans>Message (optional)</Trans>
          </Label>
          <Textarea
            id="reschedule-reason"
            name="reason"
            autoFocus={true}
            value={reason}
            placeholder={t`Let the attendee know why you need to reschedule`}
            onChange={(event) => setReason(event.currentTarget.value)}
          />
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending}>
          <Trans>Send request</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

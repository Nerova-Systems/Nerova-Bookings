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

export function CancelBookingDialog({
  booking,
  isOpen,
  onOpenChange,
  onCancelled
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onCancelled?: () => void;
}>) {
  return (
    <Dialog trackingTitle={t`Cancel booking`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Cancel event?</Trans>
          </DialogTitle>
          <DialogDescription>
            {booking ? (
              <Trans>This cancels the booking with {booking.bookerName}.</Trans>
            ) : (
              <Trans>This cancels the booking.</Trans>
            )}
          </DialogDescription>
        </DialogHeader>
        {booking && (
          <CancelBookingDialogBody booking={booking} onClose={() => onOpenChange(false)} onCancelled={onCancelled} />
        )}
      </DialogContent>
    </Dialog>
  );
}

function CancelBookingDialogBody({
  booking,
  onClose,
  onCancelled
}: Readonly<{ booking: BookingListItem; onClose: () => void; onCancelled?: () => void }>) {
  const [reason, setReason] = useState("");
  const cancelMutation = api.useMutation("post", "/api/bookings/{id}/cancel", {
    onSuccess: () => {
      toast.success(t`Booking cancelled`);
      void queryClient.invalidateQueries();
      onClose();
      onCancelled?.();
    }
  });

  return (
    <DialogForm
      validationErrors={cancelMutation.error?.errors}
      onSubmit={() => {
        cancelMutation.mutate({
          params: { path: { id: booking.id }, query: { reason: reason.trim() || null } }
        });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={cancelMutation.error} />
        <div className="flex flex-col gap-2">
          <Label htmlFor="cancel-reason">
            <Trans>Reason (optional)</Trans>
          </Label>
          <Textarea
            id="cancel-reason"
            name="reason"
            autoFocus={true}
            value={reason}
            placeholder={t`Let the attendee know why this booking is being cancelled`}
            onChange={(event) => setReason(event.currentTarget.value)}
          />
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={cancelMutation.isPending} />}>
          <Trans>Keep booking</Trans>
        </DialogClose>
        <Button type="submit" variant="destructive" isPending={cancelMutation.isPending}>
          <Trans>Cancel event</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

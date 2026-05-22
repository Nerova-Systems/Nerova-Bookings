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

export function RejectBookingDialog({
  booking,
  isOpen,
  onOpenChange,
  onRejected
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onRejected?: () => void;
}>) {
  return (
    <Dialog trackingTitle={t`Reject booking`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Reject booking</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>The attendee will be notified that their booking has been rejected.</Trans>
          </DialogDescription>
        </DialogHeader>
        {booking && <RejectBookingDialogBody booking={booking} onRejected={onRejected} onClose={() => onOpenChange(false)} />}
      </DialogContent>
    </Dialog>
  );
}

function RejectBookingDialogBody({
  booking,
  onClose,
  onRejected
}: Readonly<{ booking: BookingListItem; onClose: () => void; onRejected?: () => void }>) {
  const [reason, setReason] = useState("");
  const rejectMutation = api.useMutation("post", "/api/bookings/{id}/reject", {
    onSuccess: () => {
      toast.success(t`Booking rejected`);
      void queryClient.invalidateQueries();
      onClose();
      onRejected?.();
    }
  });

  return (
    <DialogForm
      validationErrors={rejectMutation.error?.errors}
      onSubmit={() => {
        if (reason.trim().length === 0) return;
        rejectMutation.mutate({ params: { path: { id: booking.id } }, body: { id: booking.id, reason: reason.trim() } });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={rejectMutation.error} />
        <div className="flex flex-col gap-2">
          <Label htmlFor="reject-reason">
            <Trans>Reason</Trans>
          </Label>
          <Textarea
            id="reject-reason"
            name="reason"
            required={true}
            autoFocus={true}
            value={reason}
            placeholder={t`Explain why this booking is being rejected`}
            onChange={(event) => setReason(event.currentTarget.value)}
          />
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={rejectMutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button
          type="submit"
          variant="destructive"
          isPending={rejectMutation.isPending}
          disabled={reason.trim().length === 0}
        >
          <Trans>Reject booking</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

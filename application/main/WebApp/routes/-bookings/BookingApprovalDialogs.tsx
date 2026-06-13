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
import { TextAreaField } from "@repo/ui/components/TextAreaField";

import { api } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { type BookingDialogProps, completeAction, nullableString } from "./bookingDialogUtils";

export function ConfirmBookingDialog({ booking, isOpen, onOpenChange, onCompleted }: BookingDialogProps) {
  const mutation = api.useMutation("post", "/api/bookings/{id}/confirm", {
    onSuccess: () => completeAction(t`Booking confirmed`, onOpenChange, onCompleted)
  });

  return (
    <Dialog trackingTitle={t`Confirm booking`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={mutation.error?.errors}
          onSubmit={(event) => {
            event.preventDefault();
            if (!booking) return;
            mutation.mutate({ params: { path: { id: booking.id } } });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Confirm booking?</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>This confirms the booking with {booking?.bookerName ?? t`this client`}.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody>
            <GeneralApiErrors error={mutation.error} />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" disabled={mutation.isPending} />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" disabled={!booking} isPending={mutation.isPending}>
              <Trans>Confirm booking</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}

export function RejectBookingDialog({ booking, isOpen, onOpenChange, onCompleted }: BookingDialogProps) {
  const mutation = api.useMutation("post", "/api/bookings/{id}/reject", {
    onSuccess: () => completeAction(t`Booking rejected`, onOpenChange, onCompleted)
  });

  return (
    <Dialog trackingTitle={t`Reject booking`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={mutation.error?.errors}
          onSubmit={(event) => {
            event.preventDefault();
            if (!booking) return;
            const formData = new FormData(event.currentTarget);
            mutation.mutate({
              params: { path: { id: booking.id } },
              body: { rejectionReason: nullableString(formData.get("rejectionReason")) }
            });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Reject booking?</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>This rejects the pending booking with {booking?.bookerName ?? t`this client`}.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="gap-4">
            <GeneralApiErrors error={mutation.error} />
            <TextAreaField name="rejectionReason" label={t`Rejection reason`} />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" disabled={mutation.isPending} />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" variant="destructive" disabled={!booking} isPending={mutation.isPending}>
              <Trans>Reject booking</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}

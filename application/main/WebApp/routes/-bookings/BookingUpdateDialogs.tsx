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
import { TextField } from "@repo/ui/components/TextField";

import { api } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";
import { type BookingDialogProps, completeAction, nullableString } from "./bookingDialogUtils";

export function RequestRescheduleDialog({ booking, isOpen, onOpenChange, onCompleted }: BookingDialogProps) {
  const mutation = api.useMutation("post", "/api/bookings/{id}/request-reschedule", {
    onSuccess: () => completeAction(t`Reschedule requested`, onOpenChange, onCompleted)
  });

  return (
    <Dialog trackingTitle={t`Request reschedule`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={mutation.error?.errors}
          onSubmit={(event) => {
            event.preventDefault();
            if (!booking) return;
            const formData = new FormData(event.currentTarget);
            mutation.mutate({
              params: { path: { id: booking.id } },
              body: { rescheduleReason: nullableString(formData.get("rescheduleReason")) }
            });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Request reschedule</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>This marks the booking as needing a new time.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="gap-4">
            <GeneralApiErrors error={mutation.error} />
            <TextAreaField name="rescheduleReason" label={t`Reschedule reason`} required={true} />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" disabled={mutation.isPending} />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" disabled={!booking} isPending={mutation.isPending}>
              <Trans>Request reschedule</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}

export function EditBookingLocationDialog({ booking, isOpen, onOpenChange, onCompleted }: BookingDialogProps) {
  const mutation = api.useMutation("put", "/api/bookings/{id}/location", {
    onSuccess: () => completeAction(t`Location updated`, onOpenChange, onCompleted)
  });

  return (
    <Dialog trackingTitle={t`Edit location`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={mutation.error?.errors}
          onSubmit={(event) => {
            event.preventDefault();
            if (!booking) return;
            const formData = new FormData(event.currentTarget);
            mutation.mutate({
              params: { path: { id: booking.id } },
              body: {
                locationType: nullableString(formData.get("locationType")),
                locationValue: nullableString(formData.get("locationValue"))
              }
            });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Edit location</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>Update where this booking takes place.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="gap-4">
            <GeneralApiErrors error={mutation.error} />
            <TextField name="locationType" label={t`Location type`} defaultValue={booking?.locationType ?? ""} />
            <TextField name="locationValue" label={t`Location`} defaultValue={booking?.locationValue ?? ""} />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" disabled={mutation.isPending} />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" disabled={!booking} isPending={mutation.isPending}>
              <Trans>Save</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}

export function AddGuestsDialog({ booking, isOpen, onOpenChange, onCompleted }: BookingDialogProps) {
  const mutation = api.useMutation("post", "/api/bookings/{id}/guests", {
    onSuccess: () => completeAction(t`Guests added`, onOpenChange, onCompleted)
  });

  return (
    <Dialog trackingTitle={t`Add guests`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={mutation.error?.errors}
          onSubmit={(event) => {
            event.preventDefault();
            if (!booking) return;
            const formData = new FormData(event.currentTarget);
            mutation.mutate({
              params: { path: { id: booking.id } },
              body: {
                id: booking.id,
                guests: [
                  {
                    name: String(formData.get("guestName") ?? ""),
                    email: String(formData.get("guestEmail") ?? ""),
                    timeZone: booking.timeZone,
                    locale: null
                  }
                ]
              }
            });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>Add guests</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>Add another client to this booking.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="gap-4">
            <GeneralApiErrors error={mutation.error} />
            <TextField name="guestName" label={t`Client name`} required={true} />
            <TextField name="guestEmail" label={t`Client email`} type="email" required={true} />
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" disabled={mutation.isPending} />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" disabled={!booking} isPending={mutation.isPending}>
              <Trans>Add guests</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}

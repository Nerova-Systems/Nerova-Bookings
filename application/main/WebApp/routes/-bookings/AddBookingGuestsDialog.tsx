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
import { TextField } from "@repo/ui/components/TextField";
import { PlusIcon, TrashIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";

type GuestDraft = { name: string; email: string };

export function AddBookingGuestsDialog({
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
    <Dialog trackingTitle={t`Add guests`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Add guests</Trans>
          </DialogTitle>
          <DialogDescription>
            <Trans>Guests will receive an invite for this booking.</Trans>
          </DialogDescription>
        </DialogHeader>
        {booking && (
          <AddBookingGuestsDialogBody booking={booking} onClose={() => onOpenChange(false)} onAdded={onAdded} />
        )}
      </DialogContent>
    </Dialog>
  );
}

function AddBookingGuestsDialogBody({
  booking,
  onClose,
  onAdded
}: Readonly<{ booking: BookingListItem; onClose: () => void; onAdded?: () => void }>) {
  const [guests, setGuests] = useState<GuestDraft[]>([{ name: "", email: "" }]);
  const mutation = api.useMutation("post", "/api/bookings/{id}/guests", {
    onSuccess: () => {
      toast.success(t`Guests added`);
      void queryClient.invalidateQueries();
      onClose();
      onAdded?.();
    }
  });

  const updateGuest = (index: number, patch: Partial<GuestDraft>) => {
    setGuests((current) => current.map((guest, position) => (position === index ? { ...guest, ...patch } : guest)));
  };

  const validGuests = guests
    .map((guest) => ({ name: guest.name.trim(), email: guest.email.trim() }))
    .filter((guest) => guest.email.length > 0);

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() => {
        if (validGuests.length === 0) return;
        mutation.mutate({
          params: { path: { id: booking.id } },
          body: {
            id: booking.id,
            guests: validGuests.map((guest) => ({
              name: guest.name || guest.email,
              email: guest.email,
              locale: null,
              timeZone: booking.timeZone
            }))
          }
        });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <div className="flex flex-col gap-3">
          {guests.map((guest, index) => (
            // biome-ignore lint/suspicious/noArrayIndexKey: Stable for the lifetime of this body.
            <div key={index} className="grid gap-2 sm:grid-cols-[1fr_1fr_auto]">
              <TextField
                name={`guest-name-${index}`}
                label={index === 0 ? t`Name` : undefined}
                value={guest.name}
                onChange={(value) => updateGuest(index, { name: value })}
              />
              <TextField
                name={`guest-email-${index}`}
                type="email"
                label={index === 0 ? t`Email` : undefined}
                value={guest.email}
                onChange={(value) => updateGuest(index, { email: value })}
              />
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label={t`Remove guest`}
                disabled={guests.length === 1}
                className="self-end"
                onClick={() => setGuests((current) => current.filter((_, position) => position !== index))}
              >
                <TrashIcon />
              </Button>
            </div>
          ))}
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="self-start"
            onClick={() => setGuests((current) => [...current, { name: "", email: "" }])}
          >
            <PlusIcon />
            <Trans>Add another guest</Trans>
          </Button>
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending} disabled={validGuests.length === 0}>
          <Trans>Add guests</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

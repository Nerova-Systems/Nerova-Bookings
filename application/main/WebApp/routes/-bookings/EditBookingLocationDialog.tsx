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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { TextField } from "@repo/ui/components/TextField";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";

const locationTypeOptions = [
  { value: "integrations:zoom", label: "Zoom" },
  { value: "integrations:google:meet", label: "Google Meet" },
  { value: "inPerson", label: "In person" },
  { value: "phone", label: "Phone" },
  { value: "link", label: "Custom link" }
] as const;

export function EditBookingLocationDialog({
  booking,
  isOpen,
  onOpenChange,
  onSaved
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onSaved?: () => void;
}>) {
  return (
    <Dialog trackingTitle={t`Edit location`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>
            <Trans>Edit location</Trans>
          </DialogTitle>
        </DialogHeader>
        {booking && <EditBookingLocationDialogBody booking={booking} onClose={() => onOpenChange(false)} onSaved={onSaved} />}
      </DialogContent>
    </Dialog>
  );
}

function EditBookingLocationDialogBody({
  booking,
  onClose,
  onSaved
}: Readonly<{ booking: BookingListItem; onClose: () => void; onSaved?: () => void }>) {
  const [locationType, setLocationType] = useState(booking.locationType ?? "link");
  const [locationValue, setLocationValue] = useState(booking.locationValue ?? "");
  const mutation = api.useMutation("post", "/api/bookings/{id}/location", {
    onSuccess: () => {
      toast.success(t`Location updated`);
      void queryClient.invalidateQueries();
      onClose();
      onSaved?.();
    }
  });

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() => {
        mutation.mutate({
          params: { path: { id: booking.id } },
          body: {
            id: booking.id,
            locationType: locationType || null,
            locationValue: locationValue.trim() || null
          }
        });
      }}
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <label htmlFor="location-type" className="text-sm font-medium">
              <Trans>Location type</Trans>
            </label>
            <Select value={locationType} onValueChange={(value) => setLocationType(value ?? "")}>
              <SelectTrigger id="location-type" aria-label={t`Location type`} className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {locationTypeOptions.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <TextField
            name="locationValue"
            label={t`Details`}
            value={locationValue}
            placeholder={t`Meeting link, address, or phone number`}
            onChange={setLocationValue}
          />
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending}>
          <Trans>Save location</Trans>
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

import type { Dispatch, SetStateAction } from "react";

import type { BookingListItem } from "./bookingTypes";

import { ConfirmBookingDialog, RejectBookingDialog } from "./BookingApprovalDialogs";
import { AddGuestsDialog, EditBookingLocationDialog, RequestRescheduleDialog } from "./BookingUpdateDialogs";
import { CancelBookingDialog } from "./CancelBookingDialog";

type DialogState = Readonly<{
  confirm: boolean;
  reject: boolean;
  requestReschedule: boolean;
  editLocation: boolean;
  addGuests: boolean;
  cancel: boolean;
}>;

export function BookingActionDialogs({
  booking,
  dialogState,
  setDialogState,
  onActionComplete
}: Readonly<{
  booking: BookingListItem;
  dialogState: DialogState;
  setDialogState: Dispatch<SetStateAction<DialogState>>;
  onActionComplete?: () => void;
}>) {
  const setOpen = (key: keyof DialogState) => (isOpen: boolean) =>
    setDialogState((current) => ({ ...current, [key]: isOpen }));

  return (
    <>
      <ConfirmBookingDialog
        booking={booking}
        isOpen={dialogState.confirm}
        onOpenChange={setOpen("confirm")}
        onCompleted={onActionComplete}
      />
      <RejectBookingDialog
        booking={booking}
        isOpen={dialogState.reject}
        onOpenChange={setOpen("reject")}
        onCompleted={onActionComplete}
      />
      <RequestRescheduleDialog
        booking={booking}
        isOpen={dialogState.requestReschedule}
        onOpenChange={setOpen("requestReschedule")}
        onCompleted={onActionComplete}
      />
      <EditBookingLocationDialog
        booking={booking}
        isOpen={dialogState.editLocation}
        onOpenChange={setOpen("editLocation")}
        onCompleted={onActionComplete}
      />
      <AddGuestsDialog
        booking={booking}
        isOpen={dialogState.addGuests}
        onOpenChange={setOpen("addGuests")}
        onCompleted={onActionComplete}
      />
      <CancelBookingDialog
        booking={booking}
        isOpen={dialogState.cancel}
        onOpenChange={setOpen("cancel")}
        onCancelled={onActionComplete}
      />
    </>
  );
}

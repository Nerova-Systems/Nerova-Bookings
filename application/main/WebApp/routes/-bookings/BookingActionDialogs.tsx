import type { BookingListItem } from "./bookingTypes";

import { AddBookingGuestsDialog } from "./AddBookingGuestsDialog";
import { AddBookingInternalNoteDialog } from "./AddBookingInternalNoteDialog";
import { CancelBookingDialog } from "./CancelBookingDialog";
import { ConfirmBookingDialog } from "./ConfirmBookingDialog";
import { EditBookingLocationDialog } from "./EditBookingLocationDialog";
import { MarkNoShowDialog } from "./MarkNoShowDialog";
import { RateBookingDialog } from "./RateBookingDialog";
import { RejectBookingDialog } from "./RejectBookingDialog";
import { RequestRescheduleDialog } from "./RequestRescheduleDialog";

export type BookingDialogKind =
  | "confirm"
  | "reject"
  | "cancel"
  | "requestReschedule"
  | "editLocation"
  | "addGuests"
  | "markNoShow"
  | "rate"
  | "addNote"
  | null;

export function BookingActionDialogs({
  booking,
  active,
  onClose,
  onActionComplete
}: Readonly<{
  booking: BookingListItem;
  active: BookingDialogKind;
  onClose: () => void;
  onActionComplete?: () => void;
}>) {
  const dialogProps = <T extends Exclude<BookingDialogKind, null>>(kind: T) => ({
    booking: active === kind ? booking : null,
    isOpen: active === kind,
    onOpenChange: (open: boolean) => {
      if (!open) onClose();
    }
  });

  return (
    <>
      <ConfirmBookingDialog {...dialogProps("confirm")} onConfirmed={onActionComplete} />
      <RejectBookingDialog {...dialogProps("reject")} onRejected={onActionComplete} />
      <CancelBookingDialog {...dialogProps("cancel")} onCancelled={onActionComplete} />
      <RequestRescheduleDialog {...dialogProps("requestReschedule")} onRequested={onActionComplete} />
      <EditBookingLocationDialog {...dialogProps("editLocation")} onSaved={onActionComplete} />
      <AddBookingGuestsDialog {...dialogProps("addGuests")} onAdded={onActionComplete} />
      <MarkNoShowDialog {...dialogProps("markNoShow")} onMarked={onActionComplete} />
      <RateBookingDialog {...dialogProps("rate")} onRated={onActionComplete} />
      <AddBookingInternalNoteDialog {...dialogProps("addNote")} onAdded={onActionComplete} />
    </>
  );
}

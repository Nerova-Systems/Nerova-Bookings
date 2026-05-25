import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { DropdownMenuContent, DropdownMenuSeparator } from "@repo/ui/components/DropdownMenu";
import {
  CheckIcon,
  CircleXIcon,
  FlagIcon,
  InfoIcon,
  MapPinIcon,
  PencilIcon,
  RefreshCcwIcon,
  SendIcon,
  StarIcon,
  StickyNoteIcon,
  UserPlusIcon,
  VideoIcon,
  VideoOffIcon,
  XIcon
} from "lucide-react";
import { toast } from "sonner";

import type { BookingDialogKind } from "./BookingActionDialogs";
import type { BookingListItem } from "./bookingTypes";

import { BookingActionGroup, BookingActionItem, type BookingActionMenuItem } from "./BookingActionMenuItems";

const ALWAYS_ENABLED = { visible: true, enabled: true, disabledReason: null } as const;

export function BookingActionsMenuContent({
  booking,
  onSelectDialog
}: Readonly<{ booking: BookingListItem; onSelectDialog: (dialog: Exclude<BookingDialogKind, null>) => void }>) {
  const confirmActions: BookingActionMenuItem[] = [
    {
      key: "confirm",
      icon: <CheckIcon />,
      label: <Trans>Confirm booking</Trans>,
      onSelect: () => onSelectDialog("confirm")
    },
    {
      key: "reject",
      icon: <XIcon />,
      label: <Trans>Reject booking</Trans>,
      variant: "destructive",
      onSelect: () => onSelectDialog("reject")
    }
  ];

  const editActions: BookingActionMenuItem[] = [
    {
      key: "requestReschedule",
      icon: <SendIcon />,
      label: <Trans>Request reschedule</Trans>,
      onSelect: () => onSelectDialog("requestReschedule")
    },
    {
      key: "editLocation",
      icon: <MapPinIcon />,
      label: <Trans>Edit location</Trans>,
      onSelect: () => onSelectDialog("editLocation")
    },
    {
      key: "addGuests",
      icon: <UserPlusIcon />,
      label: <Trans>Add guests</Trans>,
      onSelect: () => onSelectDialog("addGuests")
    },
    {
      key: "reassign",
      icon: <RefreshCcwIcon />,
      label: <Trans>Reassign host</Trans>,
      onSelect: () => onSelectDialog("reassign")
    }
  ];

  const afterEventActions: BookingActionMenuItem[] = [
    { key: "viewRecordings", icon: <VideoIcon />, label: <Trans>View recordings</Trans> },
    { key: "viewSessionDetails", icon: <InfoIcon />, label: <Trans>View session details</Trans> },
    {
      key: "markNoShow",
      icon: <VideoOffIcon />,
      label: <Trans>Mark as no-show</Trans>,
      onSelect: () => onSelectDialog("markNoShow")
    },
    { key: "rate", icon: <StarIcon />, label: <Trans>Rate booking</Trans>, onSelect: () => onSelectDialog("rate") }
  ];

  const handleCopyLink = () => {
    const url = `${window.location.origin}/booking/${booking.id}`;
    void navigator.clipboard.writeText(url);
    toast.success(t`Booking link copied`);
  };

  return (
    <DropdownMenuContent align="end" className="w-72" onClick={(event) => event.stopPropagation()}>
      <BookingActionGroup label={<Trans>Confirmation</Trans>} booking={booking} items={confirmActions} />
      <DropdownMenuSeparator />
      <BookingActionGroup label={<Trans>Edit event</Trans>} booking={booking} items={editActions} />
      <DropdownMenuSeparator />
      <BookingActionGroup label={<Trans>After event</Trans>} booking={booking} items={afterEventActions} />
      <DropdownMenuSeparator />
      <BookingActionItem
        action={ALWAYS_ENABLED}
        icon={<StickyNoteIcon />}
        label={<Trans>Add internal note</Trans>}
        trackingLabel={t`Add internal note`}
        onSelect={() => onSelectDialog("addNote")}
      />
      <BookingActionItem
        action={ALWAYS_ENABLED}
        icon={<PencilIcon />}
        label={<Trans>Copy booking link</Trans>}
        trackingLabel={t`Copy booking link`}
        onSelect={handleCopyLink}
      />
      <BookingActionItem
        action={booking.actions.report}
        icon={<FlagIcon />}
        label={<Trans>Report booking</Trans>}
        trackingLabel={t`Report booking`}
      />
      <DropdownMenuSeparator />
      <BookingActionItem
        action={booking.actions.cancel}
        icon={<CircleXIcon />}
        label={<Trans>Cancel event</Trans>}
        trackingLabel={t`Cancel booking`}
        variant="destructive"
        onSelect={() => onSelectDialog("cancel")}
      />
    </DropdownMenuContent>
  );
}

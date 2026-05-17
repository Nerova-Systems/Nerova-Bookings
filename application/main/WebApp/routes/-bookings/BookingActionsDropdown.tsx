import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import {
  CircleXIcon,
  EllipsisIcon,
  FlagIcon,
  InfoIcon,
  MapPinIcon,
  RefreshCcwIcon,
  SendIcon,
  UserPlusIcon,
  VideoIcon,
  VideoOffIcon
} from "lucide-react";
import { useState } from "react";

import type { BookingListItem } from "./bookingTypes";

import { BookingActionGroup, BookingActionItem, type BookingActionMenuItem } from "./BookingActionMenuItems";
import { CancelBookingDialog } from "./CancelBookingDialog";

export function BookingActionsDropdown({
  booking,
  align = "end",
  onActionComplete
}: Readonly<{
  booking: BookingListItem;
  align?: "start" | "center" | "end";
  onActionComplete?: () => void;
}>) {
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const editActions: BookingActionMenuItem[] = [
    {
      key: "reschedule",
      icon: <RefreshCcwIcon />,
      label: <Trans>Reschedule booking</Trans>
    },
    {
      key: "requestReschedule",
      icon: <SendIcon />,
      label: <Trans>Request reschedule</Trans>
    },
    {
      key: "editLocation",
      icon: <MapPinIcon />,
      label: <Trans>Edit location</Trans>
    },
    {
      key: "addGuests",
      icon: <UserPlusIcon />,
      label: <Trans>Add guests</Trans>
    }
  ];
  const afterEventActions: BookingActionMenuItem[] = [
    {
      key: "viewRecordings",
      icon: <VideoIcon />,
      label: <Trans>View recordings</Trans>
    },
    {
      key: "viewSessionDetails",
      icon: <InfoIcon />,
      label: <Trans>View session details</Trans>
    },
    {
      key: "markNoShow",
      icon: <VideoOffIcon />,
      label: <Trans>Mark as no-show</Trans>
    }
  ];

  return (
    <>
      <DropdownMenu trackingTitle={t`Booking actions`}>
        <DropdownMenuTrigger
          render={
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              data-testid="booking-actions-dropdown"
              onClick={(event) => event.stopPropagation()}
            >
              <EllipsisIcon />
              <span className="sr-only">
                <Trans>Booking actions</Trans>
              </span>
            </Button>
          }
        />
        <DropdownMenuContent align={align} className="w-72" onClick={(event) => event.stopPropagation()}>
          <BookingActionGroup label={<Trans>Edit event</Trans>} booking={booking} items={editActions} />
          <DropdownMenuSeparator />
          <BookingActionGroup label={<Trans>After event</Trans>} booking={booking} items={afterEventActions} />
          <DropdownMenuSeparator />
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
            onSelect={() => setCancelDialogOpen(true)}
          />
        </DropdownMenuContent>
      </DropdownMenu>
      <CancelBookingDialog
        booking={booking}
        isOpen={cancelDialogOpen}
        onOpenChange={setCancelDialogOpen}
        onCancelled={onActionComplete}
      />
    </>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuSeparator,
  DropdownMenuTrigger
} from "@repo/ui/components/DropdownMenu";
import { useNavigate } from "@tanstack/react-router";
import {
  CheckIcon,
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

import { BookingActionDialogs } from "./BookingActionDialogs";
import { BookingActionGroup, BookingActionItem, type BookingActionMenuItem } from "./BookingActionMenuItems";

export function BookingActionsDropdown({
  booking,
  align = "end",
  onActionComplete
}: Readonly<{
  booking: BookingListItem;
  align?: "start" | "center" | "end";
  onActionComplete?: () => void;
}>) {
  const navigate = useNavigate();
  const ownerEmail = import.meta.user_info_env.email;
  const [dialogState, setDialogState] = useState({
    confirm: false,
    reject: false,
    requestReschedule: false,
    editLocation: false,
    addGuests: false,
    cancel: false
  });
  const openDialog = (key: keyof typeof dialogState) =>
    setDialogState((current) => ({
      ...current,
      [key]: true
    }));
  const pendingActions: BookingActionMenuItem[] = [
    {
      key: "confirm",
      icon: <CheckIcon />,
      label: <Trans>Confirm booking</Trans>,
      onSelect: () => openDialog("confirm")
    },
    {
      key: "reject",
      icon: <CircleXIcon />,
      label: <Trans>Reject booking</Trans>,
      variant: "destructive",
      onSelect: () => openDialog("reject")
    }
  ];
  const editActions: BookingActionMenuItem[] = [
    {
      key: "reschedule",
      icon: <RefreshCcwIcon />,
      label: <Trans>Reschedule booking</Trans>,
      onSelect: () => {
        navigate({
          to: "/$handle/$eventSlug",
          params: { handle: booking.schedulingHandle, eventSlug: booking.eventTypeSlug },
          search: {
            month: undefined,
            date: undefined,
            slot: undefined,
            duration: undefined,
            timezone: undefined,
            privateLink: undefined,
            rescheduleUid: booking.id,
            rescheduledBy: ownerEmail ?? undefined
          }
        });
      }
    },
    {
      key: "requestReschedule",
      icon: <SendIcon />,
      label: <Trans>Request reschedule</Trans>,
      onSelect: () => openDialog("requestReschedule")
    },
    {
      key: "editLocation",
      icon: <MapPinIcon />,
      label: <Trans>Edit location</Trans>,
      onSelect: () => openDialog("editLocation")
    },
    {
      key: "addGuests",
      icon: <UserPlusIcon />,
      label: <Trans>Add guests</Trans>,
      onSelect: () => openDialog("addGuests")
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
          <BookingActionGroup label={<Trans>Approval</Trans>} booking={booking} items={pendingActions} />
          <DropdownMenuSeparator />
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
            onSelect={() => openDialog("cancel")}
          />
        </DropdownMenuContent>
      </DropdownMenu>
      <BookingActionDialogs
        booking={booking}
        dialogState={dialogState}
        setDialogState={setDialogState}
        onActionComplete={onActionComplete}
      />
    </>
  );
}

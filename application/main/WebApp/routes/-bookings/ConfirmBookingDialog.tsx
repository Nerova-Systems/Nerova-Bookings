import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogBody,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from "@repo/ui/components/AlertDialog";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { BookingListItem } from "./bookingTypes";

import { GeneralApiErrors } from "../-scheduling/ApiErrors";

export function ConfirmBookingDialog({
  booking,
  isOpen,
  onOpenChange,
  onConfirmed
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onConfirmed?: () => void;
}>) {
  const confirmMutation = api.useMutation("post", "/api/bookings/{id}/confirm", {
    onSuccess: () => {
      toast.success(t`Booking confirmed`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
      onConfirmed?.();
    }
  });

  return (
    <AlertDialog trackingTitle={t`Confirm booking`} open={isOpen} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            <Trans>Confirm this booking?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>The attendee will be notified that their booking is confirmed.</Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogBody>
          <GeneralApiErrors error={confirmMutation.error} />
        </AlertDialogBody>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={confirmMutation.isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            isPending={confirmMutation.isPending}
            disabled={!booking}
            onClick={() => {
              if (!booking) return;
              confirmMutation.mutate({ params: { path: { id: booking.id } } });
            }}
          >
            <Trans>Confirm booking</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

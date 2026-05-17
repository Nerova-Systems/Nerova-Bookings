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

export function CancelBookingDialog({
  booking,
  isOpen,
  onOpenChange,
  onCancelled
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onCancelled?: () => void;
}>) {
  const cancelMutation = api.useMutation("post", "/api/bookings/{id}/cancel", {
    onSuccess: () => {
      toast.success(t`Booking cancelled`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
      onCancelled?.();
    }
  });

  return (
    <AlertDialog trackingTitle={t`Cancel booking`} open={isOpen} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            <Trans>Cancel event?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>This cancels the booking with {booking?.bookerName ?? t`this attendee`}.</Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogBody>
          <GeneralApiErrors error={cancelMutation.error} />
        </AlertDialogBody>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={cancelMutation.isPending}>
            <Trans>Keep booking</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            isPending={cancelMutation.isPending}
            disabled={!booking}
            onClick={() => {
              if (!booking) return;
              cancelMutation.mutate({ params: { path: { id: booking.id } } });
            }}
          >
            <Trans>Cancel event</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

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

export function MarkNoShowDialog({
  booking,
  isOpen,
  onOpenChange,
  onMarked
}: Readonly<{
  booking: BookingListItem | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onMarked?: () => void;
}>) {
  const mutation = api.useMutation("post", "/api/bookings/{id}/no-show", {
    onSuccess: () => {
      toast.success(t`Marked as no-show`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
      onMarked?.();
    }
  });

  return (
    <AlertDialog trackingTitle={t`Mark as no-show`} open={isOpen} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            <Trans>Mark as no-show?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>This will record that the client did not show up for the meeting.</Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogBody>
          <GeneralApiErrors error={mutation.error} />
        </AlertDialogBody>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={mutation.isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            isPending={mutation.isPending}
            disabled={!booking}
            onClick={() => {
              if (!booking) return;
              mutation.mutate({
                params: { path: { id: booking.id } },
                body: { id: booking.id, attendeeId: null, noShow: true }
              });
            }}
          >
            <Trans>Mark as no-show</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

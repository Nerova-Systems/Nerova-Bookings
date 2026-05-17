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

import type { EventType } from "../schedulingTypes";

import { GeneralApiErrors } from "../ApiErrors";

export function DeleteEventTypeDialog({
  eventType,
  isOpen,
  onOpenChange,
  onDeleted
}: Readonly<{
  eventType: EventType | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onDeleted?: () => void;
}>) {
  const deleteSubject = eventType?.title ?? t`this event type`;
  const deleteMutation = api.useMutation("delete", "/api/event-types/{id}", {
    onSuccess: () => {
      toast.success(t`Event type deleted`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
      onDeleted?.();
    }
  });

  return (
    <AlertDialog trackingTitle={t`Delete event type`} open={isOpen} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            <Trans>Delete event type?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>This removes the booking page for {deleteSubject}.</Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogBody>
          <GeneralApiErrors error={deleteMutation.error} />
        </AlertDialogBody>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleteMutation.isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            isPending={deleteMutation.isPending}
            disabled={!eventType}
            onClick={() => {
              if (!eventType) return;
              deleteMutation.mutate({ params: { path: { id: eventType.id } } });
            }}
          >
            <Trans>Delete</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

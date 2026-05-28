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

import type { App } from "./appsTypes";

import { AppApiErrors } from "./AppApiErrors";

export function UninstallAppDialog({
  app,
  isOpen,
  onOpenChange
}: Readonly<{
  app: App | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}>) {
  const uninstallMutation = api.useMutation("delete", "/api/apps/{slug}/uninstall", {
    onSuccess: () => {
      toast.success(t`App uninstalled`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
    }
  });

  const handleOpenChange = (open: boolean) => {
    if (!open) uninstallMutation.reset();
    onOpenChange(open);
  };

  return (
    <AlertDialog trackingTitle={t`Uninstall app`} open={isOpen} onOpenChange={handleOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            <Trans>Uninstall {app?.name ?? ""}?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>
              Your stored connection will be removed. Existing bookings keep their location and calendar destination,
              but new bookings will no longer use this app.
            </Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogBody>
          <AppApiErrors error={uninstallMutation.error} />
        </AlertDialogBody>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={uninstallMutation.isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            isPending={uninstallMutation.isPending}
            disabled={app === null}
            onClick={() => {
              if (app === null) return;
              uninstallMutation.mutate({ params: { path: { slug: app.slug } } });
            }}
          >
            <Trans>Uninstall</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

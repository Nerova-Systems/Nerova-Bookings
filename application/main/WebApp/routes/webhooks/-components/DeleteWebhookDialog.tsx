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

import type { Webhook } from "./webhookTypes";

import { WebhookApiErrors } from "./WebhookApiErrors";
import { truncateUrl } from "./webhookTypes";

export function DeleteWebhookDialog({
  webhook,
  isOpen,
  onOpenChange,
  onDeleted
}: Readonly<{
  webhook: Webhook | null;
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  onDeleted?: () => void;
}>) {
  const deleteMutation = api.useMutation("delete", "/api/webhooks/{id}", {
    onSuccess: () => {
      toast.success(t`Webhook deleted`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
      onDeleted?.();
    }
  });

  const handleOpenChange = (open: boolean) => {
    if (!open) {
      deleteMutation.reset();
    }
    onOpenChange(open);
  };

  return (
    <AlertDialog trackingTitle={t`Delete webhook`} open={isOpen} onOpenChange={handleOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            <Trans>Delete webhook?</Trans>
          </AlertDialogTitle>
          <AlertDialogDescription>
            <Trans>
              The endpoint{" "}
              <span className="font-mono text-foreground">{truncateUrl(webhook?.targetUrl ?? "", 80)}</span> will stop
              receiving deliveries. This cannot be undone.
            </Trans>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogBody>
          <WebhookApiErrors error={deleteMutation.error} />
        </AlertDialogBody>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={deleteMutation.isPending}>
            <Trans>Cancel</Trans>
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            isPending={deleteMutation.isPending}
            disabled={!webhook}
            onClick={() => {
              if (!webhook) return;
              deleteMutation.mutate({ params: { path: { id: webhook.id } } });
            }}
          >
            <Trans>Delete</Trans>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

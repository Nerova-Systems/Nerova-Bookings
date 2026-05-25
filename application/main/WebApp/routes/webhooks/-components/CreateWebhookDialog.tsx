import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, queryClient, WebhookEventType } from "@/shared/lib/api/client";

import type { WebhookFormState } from "./WebhookFormFields";

import { WebhookApiErrors } from "./WebhookApiErrors";
import { WebhookFormFields } from "./WebhookFormFields";
import { WebhookSecretReveal } from "./WebhookSecretReveal";
import { isValidTargetUrl } from "./webhookTypes";

function emptyState(): WebhookFormState {
  return {
    targetUrl: "",
    active: true,
    eventSubscriptions: new Set<WebhookEventType>([WebhookEventType.BookingCreated]),
    eventTypeId: null
  };
}

export function CreateWebhookDialog({
  isOpen,
  onOpenChange
}: Readonly<{ isOpen: boolean; onOpenChange: (isOpen: boolean) => void }>) {
  const [state, setState] = useState<WebhookFormState>(emptyState);
  const [createdSecret, setCreatedSecret] = useState<string | null>(null);

  const { error, isPending, mutate, reset } = api.useMutation("post", "/api/webhooks", {
    onSuccess: (webhook) => {
      toast.success(t`Webhook created`);
      void queryClient.invalidateQueries();
      // Hold the dialog open on the secret-reveal step so the user can copy the value.
      setCreatedSecret(webhook.secret);
    }
  });

  useEffect(() => {
    if (isOpen) {
      setState(emptyState());
      setCreatedSecret(null);
      reset();
    }
  }, [isOpen, reset]);

  const canSubmit =
    isValidTargetUrl(state.targetUrl) && state.eventSubscriptions.size > 0 && !isPending && createdSecret === null;

  const handleClose = () => {
    onOpenChange(false);
  };

  return (
    <Dialog trackingTitle={t`Create webhook`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        {createdSecret === null ? (
          <DialogForm
            validationErrors={error?.errors}
            onSubmit={() => {
              if (!canSubmit) return;
              mutate({
                body: {
                  targetUrl: state.targetUrl.trim(),
                  active: state.active,
                  eventSubscriptions: Array.from(state.eventSubscriptions),
                  eventTypeId: state.eventTypeId
                }
              });
            }}
          >
            <DialogHeader>
              <DialogTitle>
                <Trans>New webhook</Trans>
              </DialogTitle>
              <DialogDescription>
                <Trans>Send a signed HTTP POST to your endpoint when the selected events occur.</Trans>
              </DialogDescription>
            </DialogHeader>
            <DialogBody>
              <WebhookApiErrors error={error} />
              <WebhookFormFields state={state} onChange={setState} disabled={isPending} />
            </DialogBody>
            <DialogFooter>
              <DialogClose render={<Button type="button" variant="outline" />}>
                <Trans>Cancel</Trans>
              </DialogClose>
              <Button type="submit" disabled={!canSubmit} isPending={isPending}>
                <Trans>Create webhook</Trans>
              </Button>
            </DialogFooter>
          </DialogForm>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>
                <Trans>Webhook created</Trans>
              </DialogTitle>
              <DialogDescription>
                <Trans>Use this signing secret to verify the HMAC signature on incoming webhook deliveries.</Trans>
              </DialogDescription>
            </DialogHeader>
            <DialogBody>
              <WebhookSecretReveal secret={createdSecret} />
            </DialogBody>
            <DialogFooter>
              <Button type="button" onClick={handleClose}>
                <Trans>Done</Trans>
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

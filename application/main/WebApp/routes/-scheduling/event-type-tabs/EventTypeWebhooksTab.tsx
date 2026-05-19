/* eslint-disable max-lines */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Checkbox } from "@repo/ui/components/Checkbox";
import {
  DialogBody,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { Switch } from "@repo/ui/components/Switch";
import { TextField } from "@repo/ui/components/TextField";
import { PencilIcon, PlusIcon, SendIcon, Trash2Icon, WebhookIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, queryClient, type Schemas } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../ApiErrors";
import { SideEffectDeliveriesPanel } from "./SideEffectDeliveriesPanel";

type WebhookSubscription = Schemas["WebhookSubscriptionResponse"];
type WebhookPayload = Schemas["CreateWebhookSubscriptionRequest"];

const webhookTriggers = [
  "BOOKING_CREATED",
  "BOOKING_CONFIRMED",
  "BOOKING_REJECTED",
  "BOOKING_CANCELLED",
  "BOOKING_RESCHEDULED",
  "BOOKING_LOCATION_CHANGED",
  "BOOKING_GUESTS_ADDED"
];

type EventTypeWebhooksTabProps = Readonly<{
  eventTypeId: string;
}>;

export function EventTypeWebhooksTab({ eventTypeId }: EventTypeWebhooksTabProps) {
  const { data, isLoading } = api.useQuery("get", "/api/event-types/{eventTypeId}/webhooks", {
    params: { path: { eventTypeId } }
  });
  const deleteMutation = api.useMutation("delete", "/api/event-types/{eventTypeId}/webhooks/{id}", {
    onSuccess: () => {
      toast.success(t`Webhook deleted`);
      void queryClient.invalidateQueries();
    }
  });
  const testMutation = api.useMutation("post", "/api/event-types/{eventTypeId}/webhooks/{id}/test", {
    onSuccess: () => {
      toast.success(t`Test webhook queued`);
      void queryClient.invalidateQueries();
    }
  });
  const [editingWebhook, setEditingWebhook] = useState<WebhookSubscription | null | undefined>(undefined);
  const webhooks = data?.webhooks ?? [];

  return (
    <section className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <h2>
            <Trans>Webhooks</Trans>
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            <Trans>Send Cal.com-compatible booking lifecycle payloads to external systems.</Trans>
          </p>
        </div>
        <Button type="button" size="sm" onClick={() => setEditingWebhook(null)}>
          <PlusIcon />
          <Trans>Add webhook</Trans>
        </Button>
      </div>
      <GeneralApiErrors error={deleteMutation.error ?? testMutation.error} />
      {isLoading ? (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>Loading webhooks...</Trans>
        </div>
      ) : webhooks.length === 0 ? (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>No webhooks configured.</Trans>
        </div>
      ) : (
        <div className="grid gap-3">
          {webhooks.map((webhook) => (
            <div key={webhook.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-4">
              <div className="flex min-w-0 items-start gap-3">
                <WebhookIcon className="mt-0.5 size-4 text-muted-foreground" />
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium break-all">{webhook.subscriberUrl}</span>
                    <span className="rounded-md bg-muted px-2 py-1 text-xs text-muted-foreground">
                      {webhook.active ? t`Active` : t`Disabled`}
                    </span>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">{webhook.triggers.join(", ")}</p>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={testMutation.isPending}
                  onClick={() => testMutation.mutate({ params: { path: { eventTypeId, id: webhook.id } } })}
                >
                  <SendIcon />
                  <Trans>Test</Trans>
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => setEditingWebhook(webhook)}>
                  <PencilIcon />
                  <Trans>Edit</Trans>
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={deleteMutation.isPending}
                  onClick={() => deleteMutation.mutate({ params: { path: { eventTypeId, id: webhook.id } } })}
                >
                  <Trash2Icon />
                  <Trans>Delete</Trans>
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
      <SideEffectDeliveriesPanel eventTypeId={eventTypeId} kind="webhook" />
      <WebhookDialog eventTypeId={eventTypeId} webhook={editingWebhook} onOpenChange={setEditingWebhook} />
    </section>
  );
}

function WebhookDialog({
  eventTypeId,
  webhook,
  onOpenChange
}: Readonly<{
  eventTypeId: string;
  webhook: WebhookSubscription | null | undefined;
  onOpenChange: (webhook: WebhookSubscription | null | undefined) => void;
}>) {
  const isOpen = webhook !== undefined;
  return (
    <DirtyDialog open={isOpen} onOpenChange={(open) => !open && onOpenChange(undefined)} trackingTitle={t`Webhook`}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>{webhook ? <Trans>Edit webhook</Trans> : <Trans>Add webhook</Trans>}</DialogTitle>
        </DialogHeader>
        {isOpen && (
          <WebhookDialogBody eventTypeId={eventTypeId} webhook={webhook} onClose={() => onOpenChange(undefined)} />
        )}
      </DialogContent>
    </DirtyDialog>
  );
}

function WebhookDialogBody({
  eventTypeId,
  webhook,
  onClose
}: Readonly<{
  eventTypeId: string;
  webhook: WebhookSubscription | null;
  onClose: () => void;
}>) {
  const setDirty = useDialogSetDirty();
  const [payload, setPayload] = useState<WebhookPayload>(() => webhookToPayload(webhook));
  const createMutation = api.useMutation("post", "/api/event-types/{eventTypeId}/webhooks", {
    onSuccess: () => {
      toast.success(t`Webhook saved`);
      void queryClient.invalidateQueries();
      onClose();
    }
  });
  const updateMutation = api.useMutation("put", "/api/event-types/{eventTypeId}/webhooks/{id}", {
    onSuccess: () => {
      toast.success(t`Webhook saved`);
      void queryClient.invalidateQueries();
      onClose();
    }
  });
  const mutation = webhook ? updateMutation : createMutation;

  useEffect(() => setPayload(webhookToPayload(webhook)), [webhook]);

  const updatePayload = (nextPayload: WebhookPayload) => {
    setDirty(true);
    setPayload(nextPayload);
  };

  const toggleTrigger = (trigger: string, checked: boolean) => {
    const triggers = checked
      ? [...new Set([...payload.triggers, trigger])]
      : payload.triggers.filter((existingTrigger) => existingTrigger !== trigger);
    updatePayload({ ...payload, triggers });
  };

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() =>
        webhook
          ? updateMutation.mutate({ params: { path: { eventTypeId, id: webhook.id } }, body: payload })
          : createMutation.mutate({ params: { path: { eventTypeId } }, body: payload })
      }
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <TextField
          autoFocus
          required
          name="subscriberUrl"
          label={t`Subscriber URL`}
          value={payload.subscriberUrl}
          onChange={(subscriberUrl) => updatePayload({ ...payload, subscriberUrl })}
        />
        <TextField
          name="secret"
          label={t`Secret`}
          value={payload.secret ?? ""}
          onChange={(secret) => updatePayload({ ...payload, secret: secret.trim().length > 0 ? secret : null })}
        />
        <div className="flex items-center justify-between gap-3 rounded-md border p-3">
          <span className="text-sm font-medium">
            <Trans>Active</Trans>
          </span>
          <Switch checked={payload.active} onCheckedChange={(active) => updatePayload({ ...payload, active })} />
        </div>
        <div className="grid gap-2 rounded-md border p-3">
          <span className="text-sm font-medium">
            <Trans>Triggers</Trans>
          </span>
          {webhookTriggers.map((trigger) => (
            <label key={trigger} className="flex min-h-[var(--control-height-sm)] items-center gap-3 text-sm">
              <Checkbox
                checked={payload.triggers.includes(trigger)}
                onCheckedChange={(checked) => toggleTrigger(trigger, checked === true)}
              />
              <span>{trigger}</span>
            </label>
          ))}
        </div>
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending}>
          {mutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save webhook</Trans>}
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

function webhookToPayload(webhook: WebhookSubscription | null): WebhookPayload {
  return {
    active: webhook?.active ?? true,
    subscriberUrl: webhook?.subscriberUrl ?? "",
    secret: webhook?.secret ?? null,
    triggers: webhook?.triggers?.length ? webhook.triggers : ["BOOKING_CREATED"],
    payloadFormat: webhook?.payloadFormat ?? "cal-com",
    payloadVersion: webhook?.payloadVersion ?? "v1"
  };
}

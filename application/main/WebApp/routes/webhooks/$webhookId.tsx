import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, SendIcon, Trash2Icon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { api, queryClient, type WebhookEventType } from "@/shared/lib/api/client";

import { DeleteWebhookDialog } from "./-components/DeleteWebhookDialog";
import { WebhookApiErrors } from "./-components/WebhookApiErrors";
import { type WebhookFormState, WebhookFormFields } from "./-components/WebhookFormFields";
import { WebhookScopeCard, WebhookSecretCard } from "./-components/WebhookSecretCard";
import { WebhooksPageShell } from "./-components/WebhooksPageShell";
import { isValidTargetUrl, truncateUrl } from "./-components/webhookTypes";

export const Route = createFileRoute("/webhooks/$webhookId")({
  staticData: { trackingTitle: "Webhook details" },
  component: WebhookDetailsPage
});

function WebhookDetailsPage() {
  const { webhookId } = Route.useParams();
  const navigate = useNavigate();
  const [deleteOpen, setDeleteOpen] = useState(false);

  const { data: listData, isLoading } = api.useQuery("get", "/api/webhooks");
  const webhook = listData?.webhooks.find((candidate) => candidate.id === webhookId) ?? null;

  const initialState = useMemo<WebhookFormState | null>(() => {
    if (!webhook) return null;
    return {
      targetUrl: webhook.targetUrl,
      active: webhook.active,
      eventSubscriptions: new Set<WebhookEventType>(webhook.eventSubscriptions),
      eventTypeId: webhook.eventTypeId
    };
  }, [webhook]);

  const [state, setState] = useState<WebhookFormState | null>(null);
  useEffect(() => {
    if (initialState !== null) {
      setState(initialState);
    }
  }, [initialState]);

  const updateMutation = api.useMutation("put", "/api/webhooks/{id}", {
    onSuccess: () => {
      toast.success(t`Webhook updated`);
      void queryClient.invalidateQueries();
    }
  });

  const testMutation = api.useMutation("post", "/api/webhooks/{id}/test", {
    onSuccess: () => {
      toast.success(t`Test event queued for delivery`);
    },
    onError: () => {
      toast.error(t`Failed to queue test event`);
    }
  });

  if (isLoading || !webhook || !state) {
    return (
      <WebhooksPageShell title={t`Webhook`} subtitle={t`Loading...`}>
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>Loading webhook...</Trans>
        </div>
      </WebhooksPageShell>
    );
  }

  const isDirty =
    initialState !== null &&
    (state.targetUrl !== initialState.targetUrl ||
      state.active !== initialState.active ||
      !setsEqual(state.eventSubscriptions, initialState.eventSubscriptions));

  const canSubmit =
    isDirty && isValidTargetUrl(state.targetUrl) && state.eventSubscriptions.size > 0 && !updateMutation.isPending;

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmit) return;
    updateMutation.mutate({
      params: { path: { id: webhook.id } },
      body: {
        targetUrl: state.targetUrl.trim(),
        active: state.active,
        eventSubscriptions: Array.from(state.eventSubscriptions)
      }
    });
  };

  return (
    <WebhooksPageShell
      title={truncateUrl(webhook.targetUrl, 60)}
      subtitle={webhook.active ? t`Active` : t`Paused`}
      maxWidth="64rem"
      titleContent={
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex items-center gap-2">
            <Button variant="ghost" size="sm" onClick={() => navigate({ to: "/webhooks" })}>
              <ArrowLeftIcon />
              <Trans>Webhooks</Trans>
            </Button>
          </div>
          <h1 className="truncate font-mono text-2xl font-semibold" title={webhook.targetUrl}>
            {truncateUrl(webhook.targetUrl, 60)}
          </h1>
        </div>
      }
      actions={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            isPending={testMutation.isPending}
            onClick={() => testMutation.mutate({ params: { path: { id: webhook.id } } })}
          >
            <SendIcon />
            <Trans>Send test event</Trans>
          </Button>
          <Button variant="outline" onClick={() => setDeleteOpen(true)}>
            <Trash2Icon />
            <Trans>Delete</Trans>
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-6">
        <Card>
          <CardHeader>
            <CardTitle>
              <Trans>Configuration</Trans>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
              <WebhookApiErrors error={updateMutation.error} />
              <WebhookFormFields
                state={state}
                onChange={setState}
                hideEventTypeScope={true}
                disabled={updateMutation.isPending}
              />
              <div className="flex justify-end">
                <Button type="submit" disabled={!canSubmit} isPending={updateMutation.isPending}>
                  <Trans>Save changes</Trans>
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        <WebhookSecretCard />

        {webhook.eventTypeId !== null && <WebhookScopeCard eventTypeId={webhook.eventTypeId} />}
      </div>

      <DeleteWebhookDialog
        webhook={webhook}
        isOpen={deleteOpen}
        onOpenChange={setDeleteOpen}
        onDeleted={() => navigate({ to: "/webhooks" })}
      />
    </WebhooksPageShell>
  );
}

function setsEqual<T>(a: ReadonlySet<T>, b: ReadonlySet<T>): boolean {
  if (a.size !== b.size) return false;
  for (const value of a) {
    if (!b.has(value)) return false;
  }
  return true;
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Link as RouterLink, createFileRoute } from "@tanstack/react-router";
import { PlusIcon, WebhookIcon } from "lucide-react";
import { useState } from "react";

import { api } from "@/shared/lib/api/client";

import { CreateWebhookDialog } from "./-components/CreateWebhookDialog";
import { WebhooksPageShell } from "./-components/WebhooksPageShell";
import { getWebhookEventTypeLabel, truncateUrl } from "./-components/webhookTypes";

export const Route = createFileRoute("/webhooks/")({
  staticData: { trackingTitle: "Webhooks" },
  component: WebhooksPage
});

function WebhooksPage() {
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const { data, isLoading } = api.useQuery("get", "/api/webhooks");
  const webhooks = data?.webhooks ?? [];

  return (
    <WebhooksPageShell
      title={t`Webhooks`}
      subtitle={t`Send signed HTTP notifications to your services when booking lifecycle events occur.`}
      actions={
        <Button onClick={() => setIsCreateOpen(true)}>
          <PlusIcon />
          <Trans>New webhook</Trans>
        </Button>
      }
    >
      <section className="flex min-w-0 flex-col">
        {isLoading ? (
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            <Trans>Loading webhooks...</Trans>
          </div>
        ) : webhooks.length === 0 ? (
          <Empty className="min-h-48 border">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <WebhookIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No webhooks yet</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Create a webhook to forward booking events to your own services.</Trans>
              </EmptyDescription>
            </EmptyHeader>
            <Button onClick={() => setIsCreateOpen(true)}>
              <PlusIcon />
              <Trans>New webhook</Trans>
            </Button>
          </Empty>
        ) : (
          <div className="overflow-hidden rounded-md border">
            {webhooks.map((webhook) => (
              <div
                key={webhook.id}
                className="flex flex-wrap items-center gap-4 border-b p-4 last:border-b-0 hover:bg-muted/60"
              >
                <div className="min-w-0 flex-1">
                  <RouterLink
                    to="/webhooks/$webhookId"
                    params={{ webhookId: webhook.id }}
                    className="block truncate font-mono text-sm font-medium hover:underline"
                    title={webhook.targetUrl}
                  >
                    {truncateUrl(webhook.targetUrl, 80)}
                  </RouterLink>
                  <div className="mt-2 flex flex-wrap items-center gap-1.5">
                    <Badge variant={webhook.active ? "default" : "secondary"}>
                      {webhook.active ? <Trans>Active</Trans> : <Trans>Paused</Trans>}
                    </Badge>
                    {webhook.eventSubscriptions.map((eventType) => (
                      <Badge key={eventType} variant="outline">
                        {getWebhookEventTypeLabel(eventType)}
                      </Badge>
                    ))}
                  </div>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  render={<RouterLink to="/webhooks/$webhookId" params={{ webhookId: webhook.id }} />}
                >
                  <Trans>Edit</Trans>
                </Button>
              </div>
            ))}
          </div>
        )}
      </section>
      <CreateWebhookDialog isOpen={isCreateOpen} onOpenChange={setIsCreateOpen} />
    </WebhooksPageShell>
  );
}

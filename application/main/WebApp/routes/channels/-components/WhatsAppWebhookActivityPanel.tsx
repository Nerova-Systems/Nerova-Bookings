import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { RadioIcon } from "lucide-react";

import type { Schemas } from "@/shared/lib/api/client";

import { api } from "@/shared/lib/api/client";

type WebhookEventItem = Schemas["WhatsAppWebhookEventItem"];

function statusBadge(status: string) {
  if (status === "Processed") return <Badge variant="default"><Trans>Processed</Trans></Badge>;
  if (status === "Failed") return <Badge variant="destructive"><Trans>Failed</Trans></Badge>;
  return <Badge variant="secondary"><Trans>Pending</Trans></Badge>;
}

export function WhatsAppWebhookActivityPanel() {
  const eventsQuery = api.useQuery("get", "/api/main/whatsapp/webhook/events", {}, { refetchInterval: 10000 });
  const formatDate = useFormatDate();

  if (eventsQuery.isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>
            <Trans>Webhook activity</Trans>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-2">
            {[...Array(3)].map((_, i) => (
              <Skeleton key={`skel-${i}`} className="h-10 w-full rounded-md" />
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  const events: WebhookEventItem[] = eventsQuery.data?.events ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Webhook activity</Trans>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {events.length === 0 ? (
          <Empty>
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <RadioIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No webhooks received</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>
                  Meta has not delivered any webhooks to this server yet. Ensure the webhook URL and verify token are
                  registered in the Meta app dashboard, and that the WhatsApp product subscription is active.
                </Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="flex flex-col gap-2">
            {events.map((event) => (
              <div key={event.id} className="flex flex-col gap-1 rounded-md border p-3">
                <div className="flex items-center justify-between gap-2">
                  {statusBadge(event.status)}
                  <time className="text-xs text-muted-foreground" dateTime={event.createdAt}>
                    {formatDate(event.createdAt, true)}
                  </time>
                </div>
                {event.error && (
                  <p className="text-xs text-destructive">{event.error}</p>
                )}
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

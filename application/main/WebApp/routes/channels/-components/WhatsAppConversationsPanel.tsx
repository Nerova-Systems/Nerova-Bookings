import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { MessagesSquareIcon } from "lucide-react";

import type { Schemas } from "@/shared/lib/api/client";

import { api } from "@/shared/lib/api/client";

type WhatsAppConversationItem = Schemas["WhatsAppConversationItem"];

function stateBadgeVariant(state: string): "default" | "secondary" | "warning" | "outline" {
  switch (state) {
    case "Confirmed":
      return "default";
    case "AwaitingFlowCompletion":
      return "warning";
    case "Expired":
      return "outline";
    default:
      return "secondary";
  }
}

function ConversationRow({
  conversation,
  formatDate
}: Readonly<{ conversation: WhatsAppConversationItem; formatDate: (input: string, includeTime: boolean) => string }>) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border px-3 py-2.5">
      <div className="flex min-w-0 flex-col gap-0.5">
        <span className="truncate text-sm font-medium">{conversation.customerPhoneNumber}</span>
        <span className="text-xs text-muted-foreground">
          <Trans>
            {conversation.inboundCount} received · {conversation.outboundCount} sent
          </Trans>
          {conversation.bookingId && (
            <>
              {" · "}
              <Trans>booked</Trans>
            </>
          )}
        </span>
      </div>
      <div className="flex flex-col items-end gap-1">
        <Badge variant={stateBadgeVariant(conversation.state)}>{conversation.state}</Badge>
        <time className="text-xs text-muted-foreground" dateTime={conversation.lastActivityAt}>
          {formatDate(conversation.lastActivityAt, true)}
        </time>
      </div>
    </div>
  );
}

export function WhatsAppConversationsPanel() {
  const conversationsQuery = api.useQuery(
    "get",
    "/api/main/whatsapp/conversations",
    {},
    { refetchInterval: 5000, refetchOnWindowFocus: true }
  );
  const formatDate = useFormatDate();

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Booking conversations</Trans>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {conversationsQuery.isLoading ? (
          <div className="flex flex-col gap-2">
            <Skeleton className="h-14 w-full" />
            <Skeleton className="h-14 w-full" />
            <Skeleton className="h-14 w-full" />
          </div>
        ) : conversationsQuery.isError ? (
          <p className="text-sm text-destructive">
            <Trans>Failed to load conversations.</Trans>
          </p>
        ) : (conversationsQuery.data?.conversations.length ?? 0) === 0 ? (
          <Empty>
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <MessagesSquareIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No conversations yet</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>When customers message your WhatsApp number, their booking conversations appear here.</Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="flex flex-col gap-2">
            {conversationsQuery.data?.conversations.map((conversation) => (
              <ConversationRow key={conversation.id} conversation={conversation} formatDate={formatDate} />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

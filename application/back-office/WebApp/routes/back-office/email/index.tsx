import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { MailIcon, RefreshCwIcon } from "lucide-react";
import { toast } from "sonner";

import { api, type components } from "@/shared/lib/api/client";

type TransactionalEmailMessage = components["schemas"]["TransactionalEmailMessageResponse"];

export const Route = createFileRoute("/back-office/email/")({
  staticData: { trackingTitle: "Email log" },
  component: EmailLogPage
});

function statusVariant(status: TransactionalEmailMessage["status"]) {
  if (status === "DeadLettered") {
    return "destructive";
  }
  if (status === "Sent") {
    return "default";
  }
  return "secondary";
}

function EmailRows({ messages }: { messages: TransactionalEmailMessage[] }) {
  const queryClient = useQueryClient();
  const formatDate = useFormatDate();
  const retryMutation = api.useMutation("post", "/api/back-office/email/messages/{id}/retry", {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["get", "/api/back-office/email/messages"] });
      toast.success(t`Email scheduled for retry`);
    }
  });

  return messages.map((message) => (
    <TableRow key={message.id}>
      <TableCell>
        <div className="font-medium">{message.subject}</div>
        <div className="text-xs text-muted-foreground">{message.recipient}</div>
      </TableCell>
      <TableCell>{message.templateKey}</TableCell>
      <TableCell>
        <Badge variant={statusVariant(message.status)}>{message.status}</Badge>
      </TableCell>
      <TableCell>{message.attempts}</TableCell>
      <TableCell>{formatDate(message.createdAt, true)}</TableCell>
      <TableCell>{message.sentAt ? formatDate(message.sentAt, true) : ""}</TableCell>
      <TableCell>
        <div className="max-w-80 truncate text-sm text-muted-foreground">{message.lastError ?? ""}</div>
      </TableCell>
      <TableCell className="text-right">
        {message.status !== "Sent" && (
          <Button
            size="sm"
            variant="secondary"
            onClick={() => retryMutation.mutate({ params: { path: { id: message.id } } })}
            isPending={retryMutation.isPending}
          >
            <RefreshCwIcon />
            <Trans>Retry</Trans>
          </Button>
        )}
      </TableCell>
    </TableRow>
  ));
}

export default function EmailLogPage() {
  const { data, isLoading } = api.useQuery("get", "/api/back-office/email/messages", { params: { query: { PageSize: 50 } } });
  const messages = data?.messages ?? [];

  return (
    <AppLayout
      variant="center"
      maxWidth="78rem"
      browserTitle={t`Email log`}
      title={t`Email log`}
      subtitle={t`Inspect transactional email delivery state and retry failed sends.`}
    >
      <Table rowSize="compact">
        <TableHeader>
          <TableRow>
            <TableHead>
              <Trans>Message</Trans>
            </TableHead>
            <TableHead>
              <Trans>Template</Trans>
            </TableHead>
            <TableHead>
              <Trans>Status</Trans>
            </TableHead>
            <TableHead>
              <Trans>Attempts</Trans>
            </TableHead>
            <TableHead>
              <Trans>Created</Trans>
            </TableHead>
            <TableHead>
              <Trans>Sent</Trans>
            </TableHead>
            <TableHead>
              <Trans>Last error</Trans>
            </TableHead>
            <TableHead className="text-right">
              <Trans>Actions</Trans>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>{!isLoading && <EmailRows messages={messages} />}</TableBody>
      </Table>

      {!isLoading && messages.length === 0 && (
        <Empty className="mt-6">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <MailIcon />
            </EmptyMedia>
            <EmptyTitle>
              <Trans>No email messages</Trans>
            </EmptyTitle>
            <EmptyDescription>
              <Trans>Queued login, invite, and billing emails will appear here.</Trans>
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}
    </AppLayout>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Input } from "@repo/ui/components/Input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { useFormatDate } from "@repo/ui/hooks/useSmartDate";
import { keepPreviousData, useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link as RouterLink, useNavigate } from "@tanstack/react-router";
import { InboxIcon, RefreshCwIcon, SearchIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { z } from "zod";

import { api, type components } from "@/shared/lib/api/client";

type OutboxMessageSummary = components["schemas"]["OutboxMessageSummary"];
type OutboxMessageStatusEnum = components["schemas"]["OutboxMessageStatus"];
type OutboxMessageStatus = `${OutboxMessageStatusEnum}`;
type OutboxStatusFilter = OutboxMessageStatus | "All";

const statuses: OutboxStatusFilter[] = ["All", "DeadLettered", "Pending", "Scheduled", "Locked", "Processed"];

const outboxSearchSchema = z.object({
  search: z.string().optional(),
  status: z.enum(["All", "Pending", "Scheduled", "Locked", "Processed", "DeadLettered"]).optional(),
  pageOffset: z.coerce.number().optional()
});

export const Route = createFileRoute("/back-office/outbox/")({
  staticData: { trackingTitle: "Outbox" },
  component: OutboxPage,
  validateSearch: outboxSearchSchema
});

function statusVariant(status: OutboxMessageStatus) {
  if (status === "DeadLettered") {
    return "destructive";
  }
  if (status === "Processed") {
    return "default";
  }
  return "secondary";
}

function formatType(type: string) {
  return type.split(".").at(-1) ?? type;
}

function OutboxRows({ messages }: { messages: OutboxMessageSummary[] }) {
  const queryClient = useQueryClient();
  const formatDate = useFormatDate();
  const retryMutation = api.useMutation("post", "/api/back-office/outbox/messages/{id}/retry", {
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["get", "/api/back-office/outbox/messages"] });
      toast.success(t`Outbox message scheduled for retry`);
    }
  });

  return messages.map((message) => {
    const canRetry = message.status !== "Processed";

    return (
      <TableRow key={message.id}>
        <TableCell>
          <div className="font-medium">{formatType(message.type)}</div>
          <div className="text-xs text-muted-foreground">{message.id}</div>
        </TableCell>
        <TableCell>
          <Badge variant={statusVariant(message.status)}>{message.status}</Badge>
        </TableCell>
        <TableCell>{message.attempts}</TableCell>
        <TableCell>{formatDate(message.nextAttemptAt, true)}</TableCell>
        <TableCell>
          <div className="max-w-96 truncate text-sm text-muted-foreground">{message.lastError ?? ""}</div>
        </TableCell>
        <TableCell className="text-right">
          {canRetry && (
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
    );
  });
}

export default function OutboxPage() {
  const navigate = useNavigate({ from: Route.fullPath });
  const { search, status = "All", pageOffset = 0 } = Route.useSearch();
  const [searchValue, setSearchValue] = useState(search ?? "");
  const activeStatus = status === "All" ? undefined : (status as OutboxMessageStatusEnum);
  const { data, isLoading } = api.useQuery(
    "get",
    "/api/back-office/outbox/messages",
    { params: { query: { Search: search, Status: activeStatus, PageOffset: pageOffset, PageSize: 25 } } },
    { placeholderData: keepPreviousData }
  );

  const messages = data?.messages ?? [];
  const hasFilter = Boolean(search) || status !== "All";

  return (
    <AppLayout
      variant="center"
      maxWidth="78rem"
      browserTitle={t`Outbox`}
      title={t`Outbox`}
      subtitle={t`Monitor message delivery health and retry unprocessed messages.`}
    >
      <div className="mb-4 flex flex-col gap-3">
        <form
          className="flex flex-col gap-2 sm:flex-row"
          onSubmit={(event) => {
            event.preventDefault();
            navigate({ search: () => ({ search: searchValue || undefined, status, pageOffset: 0 }) });
          }}
        >
          <div className="relative min-w-0 flex-1">
            <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-8"
              value={searchValue}
              onChange={(event) => setSearchValue(event.currentTarget.value)}
              placeholder={t`Search message type or error`}
            />
          </div>
          <Button type="submit" variant="secondary">
            <SearchIcon />
            <Trans>Search</Trans>
          </Button>
        </form>

        <div className="flex flex-wrap gap-2">
          {statuses.map((option) => (
            <Button
              key={option}
              size="sm"
              variant={status === option ? "default" : "secondary"}
              render={<RouterLink to="/back-office/outbox" search={{ search, status: option, pageOffset: 0 }} />}
            >
              {option === "All" ? <Trans>All</Trans> : option}
            </Button>
          ))}
        </div>
      </div>

      <Table rowSize="compact">
        <TableHeader>
          <TableRow>
            <TableHead>
              <Trans>Message</Trans>
            </TableHead>
            <TableHead>
              <Trans>Status</Trans>
            </TableHead>
            <TableHead>
              <Trans>Attempts</Trans>
            </TableHead>
            <TableHead>
              <Trans>Next attempt</Trans>
            </TableHead>
            <TableHead>
              <Trans>Last error</Trans>
            </TableHead>
            <TableHead className="text-right">
              <Trans>Actions</Trans>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>{!isLoading && <OutboxRows messages={messages} />}</TableBody>
      </Table>

      {!isLoading && messages.length === 0 && (
        <Empty className="mt-6">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <InboxIcon />
            </EmptyMedia>
            <EmptyTitle>{hasFilter ? <Trans>No matching messages</Trans> : <Trans>No outbox messages</Trans>}</EmptyTitle>
            <EmptyDescription>
              {hasFilter ? <Trans>Adjust the filters and try again.</Trans> : <Trans>Outbox messages will appear as platform events are enqueued.</Trans>}
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}
    </AppLayout>
  );
}

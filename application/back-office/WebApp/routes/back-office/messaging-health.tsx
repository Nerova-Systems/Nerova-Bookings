import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { Table, TableBody, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { createFileRoute, Link as RouterLink } from "@tanstack/react-router";
import { ActivityIcon, InboxIcon, RadioTowerIcon, TriangleAlertIcon } from "lucide-react";

import {
  formatAge,
  statusVariant,
  StoreHealthCards,
  SubscriptionRows,
  SubscriptionSkeleton,
  SummaryCard,
  SummarySkeleton
} from "./-messaging-health-components";
import { api } from "@/shared/lib/api/client";

export const Route = createFileRoute("/back-office/messaging-health")({
  staticData: { trackingTitle: "Messaging health" },
  component: MessagingHealthPage
});

export default function MessagingHealthPage() {
  const { data, isLoading } = api.useQuery("get", "/api/back-office/messaging/health");
  const totalPending = (data?.legacyOutbox.pendingCount ?? 0) + (data?.massTransitOutbox.pendingCount ?? 0);
  const oldestPendingAge = Math.max(data?.legacyOutbox.oldestPendingAgeSeconds ?? 0, data?.massTransitOutbox.oldestPendingAgeSeconds ?? 0);
  const totalDeadLetters =
    data?.broker.subscriptions.reduce(
      (total, subscription) => total + subscription.deadLetterMessageCount + subscription.transferDeadLetterMessageCount,
      0
    ) ?? 0;

  return (
    <AppLayout
      variant="center"
      maxWidth="78rem"
      browserTitle={t`Messaging health`}
      title={t`Messaging`}
      subtitle={t`Monitor local outbox stores and Azure Service Bus catalog subscriptions.`}
    >
      <div className="mb-4 flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
        <div className="flex items-center gap-2">
          <span className="text-sm text-muted-foreground">
            <Trans>Overall status</Trans>
          </span>
          {data ? <Badge variant={statusVariant(data.status)}>{data.status}</Badge> : <Skeleton className="h-5 w-20" />}
        </div>
        <Button variant="secondary" nativeButton={false} render={<RouterLink to="/back-office/outbox" />}>
          <InboxIcon />
          <Trans>View outbox rows</Trans>
        </Button>
      </div>

      <div className="mb-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {isLoading ? (
          <SummarySkeleton />
        ) : (
          <>
            <SummaryCard label={t`Broker status`} value={data?.broker.status ?? t`Unavailable`} icon={RadioTowerIcon} />
            <SummaryCard label={t`Pending outbox`} value={totalPending} icon={InboxIcon} />
            <SummaryCard label={t`Oldest pending`} value={formatAge(oldestPendingAge)} icon={ActivityIcon} />
            <SummaryCard label={t`Dead letters`} value={totalDeadLetters} icon={TriangleAlertIcon} />
          </>
        )}
      </div>

      <StoreHealthCards data={data} />

      <Table rowSize="compact">
        <TableHeader>
          <TableRow>
            <TableHead>
              <Trans>Subscription</Trans>
            </TableHead>
            <TableHead>
              <Trans>Status</Trans>
            </TableHead>
            <TableHead>
              <Trans>Active</Trans>
            </TableHead>
            <TableHead>
              <Trans>DLQ</Trans>
            </TableHead>
            <TableHead>
              <Trans>Transfer DLQ</Trans>
            </TableHead>
            <TableHead>
              <Trans>Total</Trans>
            </TableHead>
            <TableHead>
              <Trans>Error</Trans>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>{isLoading ? <SubscriptionSkeleton /> : data && <SubscriptionRows health={data} />}</TableBody>
      </Table>
    </AppLayout>
  );
}

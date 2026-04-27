import { t } from "@lingui/core/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { TableCell, TableRow } from "@repo/ui/components/Table";
import type { LucideIcon } from "lucide-react";

import type { components } from "@/shared/lib/api/client";

export type MessagingHealthResponse = components["schemas"]["MessagingHealthResponse"];
type MessagingHealthStatusEnum = components["schemas"]["MessagingHealthStatus"];
type MessagingHealthStatus = `${MessagingHealthStatusEnum}`;

export function statusVariant(status: MessagingHealthStatus) {
  if (status === "Degraded" || status === "Unavailable") {
    return "destructive";
  }
  if (status === "Warning") {
    return "secondary";
  }
  return "default";
}

export function formatAge(seconds: number) {
  if (seconds <= 0) {
    return t`None`;
  }

  const minutes = Math.floor(seconds / 60);
  if (minutes < 1) {
    return t`${seconds}s`;
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 1) {
    return t`${minutes}m`;
  }

  return t`${hours}h ${minutes % 60}m`;
}

export function SummaryCard({ label, value, icon: Icon }: { label: string; value: string | number; icon: LucideIcon }) {
  return (
    <div className="rounded-md border border-border p-4">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="text-sm text-muted-foreground">{label}</div>
          <div className="mt-1 truncate text-2xl font-semibold">{value}</div>
        </div>
        <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-muted">
          <Icon className="size-5" />
        </div>
      </div>
    </div>
  );
}

export function SummarySkeleton() {
  return Array.from({ length: 4 }, (_, index) => (
    <div key={index} className="rounded-md border border-border p-4">
      <Skeleton className="h-5 w-32" />
      <Skeleton className="mt-2 h-8 w-20" />
    </div>
  ));
}

export function StoreHealthCards({ data }: { data?: MessagingHealthResponse }) {
  return (
    <div className="mb-6 grid gap-3 md:grid-cols-3">
      <StoreHealthCard
        title={t`Legacy outbox`}
        status={data?.legacyOutbox.status}
        rows={[
          [t`Pending`, data?.legacyOutbox.pendingCount ?? 0],
          [t`Scheduled`, data?.legacyOutbox.scheduledCount ?? 0],
          [t`Locked`, data?.legacyOutbox.lockedCount ?? 0],
          [t`Dead-lettered`, data?.legacyOutbox.deadLetteredCount ?? 0]
        ]}
      />
      <StoreHealthCard
        title={t`MassTransit outbox`}
        status={data?.massTransitOutbox.status}
        rows={[
          [t`Pending`, data?.massTransitOutbox.pendingCount ?? 0],
          [t`Scheduled`, data?.massTransitOutbox.scheduledCount ?? 0],
          [t`Processed`, data?.massTransitOutbox.processedCount ?? 0],
          [t`Oldest pending`, formatAge(data?.massTransitOutbox.oldestPendingAgeSeconds ?? 0)]
        ]}
      />
      <StoreHealthCard
        title={t`MassTransit inbox`}
        status={data?.massTransitInbox.status}
        rows={[
          [t`Rows`, data?.massTransitInbox.duplicateDetectionRowCount ?? 0],
          [t`Latest received`, data?.massTransitInbox.latestReceivedAt ? t`Available` : t`None`]
        ]}
      />
    </div>
  );
}

function StoreHealthCard({
  title,
  status,
  rows
}: {
  title: string;
  status?: MessagingHealthStatus;
  rows: Array<[string, string | number]>;
}) {
  return (
    <div className="rounded-md border border-border p-4">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-base font-semibold">{title}</h2>
        {status && <Badge variant={statusVariant(status)}>{status}</Badge>}
      </div>
      <dl className="mt-3 grid grid-cols-2 gap-2 text-sm">
        {rows.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="text-right">{value}</dd>
          </div>
        ))}
      </dl>
    </div>
  );
}

export function SubscriptionRows({ health }: { health: MessagingHealthResponse }) {
  return health.broker.subscriptions.map((subscription) => (
    <TableRow key={`${subscription.topicName}-${subscription.subscriptionName}`}>
      <TableCell>
        <div className="font-medium">{subscription.topicName}</div>
        <div className="text-xs text-muted-foreground">{subscription.subscriptionName}</div>
      </TableCell>
      <TableCell>
        <Badge variant={statusVariant(subscription.status)}>{subscription.status}</Badge>
      </TableCell>
      <TableCell>{subscription.activeMessageCount}</TableCell>
      <TableCell>{subscription.deadLetterMessageCount}</TableCell>
      <TableCell>{subscription.transferDeadLetterMessageCount}</TableCell>
      <TableCell>{subscription.totalMessageCount}</TableCell>
      <TableCell>
        <div className="max-w-80 truncate text-sm text-muted-foreground">{subscription.error ?? ""}</div>
      </TableCell>
    </TableRow>
  ));
}

export function SubscriptionSkeleton() {
  return Array.from({ length: 4 }, (_, index) => (
    <TableRow key={index}>
      <TableCell>
        <Skeleton className="h-9 w-64" />
      </TableCell>
      <TableCell>
        <Skeleton className="h-5 w-20" />
      </TableCell>
      {Array.from({ length: 4 }, (_, countIndex) => (
        <TableCell key={countIndex}>
          <Skeleton className="h-5 w-10" />
        </TableCell>
      ))}
      <TableCell>
        <Skeleton className="h-5 w-32" />
      </TableCell>
    </TableRow>
  ));
}

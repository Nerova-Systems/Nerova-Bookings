import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback } from "@repo/ui/components/Avatar";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";
import { TablePagination } from "@repo/ui/components/TablePagination";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { format, formatDistanceToNow, parseISO } from "date-fns";
import { ChevronDownIcon, ChevronRightIcon, FileClockIcon } from "lucide-react";
import { useState } from "react";

import type { Schemas } from "@/shared/lib/api/client";

import { AuditLogTableSkeleton } from "./AuditLogTableSkeleton";

type AuditLogEntry = Schemas["AuditLogEntryResponse"];

interface AuditLogTableProps {
  entries: AuditLogEntry[];
  isLoading: boolean;
  hasFilters: boolean;
  totalPages: number;
  currentPageOffset: number;
  onPageChange: (page: number) => void;
}

export function AuditLogTable({
  entries,
  isLoading,
  hasFilters,
  totalPages,
  currentPageOffset,
  onPageChange
}: Readonly<AuditLogTableProps>) {
  if (isLoading && entries.length === 0) {
    return <AuditLogTableSkeleton />;
  }

  if (!isLoading && entries.length === 0) {
    return (
      <Empty>
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <FileClockIcon />
          </EmptyMedia>
          <EmptyTitle>
            {hasFilters ? <Trans>No entries match your filters</Trans> : <Trans>No audit entries yet</Trans>}
          </EmptyTitle>
          <EmptyDescription>
            {hasFilters ? (
              <Trans>Try clearing or adjusting the filters to see more results.</Trans>
            ) : (
              <Trans>Significant actions across your organization will appear here once they happen.</Trans>
            )}
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }

  const currentPage = currentPageOffset + 1;

  return (
    <>
      <div className="rounded-md bg-background">
        <Table rowSize="compact" aria-label={t`Audit log entries`}>
          <TableHeader>
            <TableRow>
              <TableHead className="w-8" />
              <TableHead>
                <Trans>Timestamp</Trans>
              </TableHead>
              <TableHead>
                <Trans>Actor</Trans>
              </TableHead>
              <TableHead>
                <Trans>Action</Trans>
              </TableHead>
              <TableHead>
                <Trans>Resource</Trans>
              </TableHead>
              <TableHead>
                <Trans>IP address</Trans>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entries.map((entry) => (
              <AuditLogTableRow key={entry.id} entry={entry} />
            ))}
          </TableBody>
        </Table>
      </div>

      {totalPages > 1 && (
        <div className="shrink-0 pt-4">
          <TablePagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={onPageChange}
            previousLabel={t`Previous`}
            nextLabel={t`Next`}
            trackingTitle="Audit log"
            className="w-full"
          />
        </div>
      )}
    </>
  );
}

function AuditLogTableRow({ entry }: Readonly<{ entry: AuditLogEntry }>) {
  const [isExpanded, setIsExpanded] = useState(false);
  const createdAt = parseISO(entry.createdAt);
  const relativeTimestamp = formatDistanceToNow(createdAt, { addSuffix: true });
  const absoluteTimestamp = format(createdAt, "yyyy-MM-dd HH:mm:ss");
  const actorInitial = entry.actorEmail.charAt(0).toUpperCase();
  const resourceLabel = entry.resourceId ? `${entry.resource} · ${entry.resourceId}` : entry.resource;

  return (
    <>
      <TableRow>
        <TableCell>
          <Button
            variant="ghost"
            size="icon-sm"
            onClick={() => setIsExpanded((value) => !value)}
            aria-label={isExpanded ? t`Collapse details` : t`Expand details`}
            aria-expanded={isExpanded}
          >
            {isExpanded ? <ChevronDownIcon className="size-4" /> : <ChevronRightIcon className="size-4" />}
          </Button>
        </TableCell>
        <TableCell>
          <Tooltip>
            <TooltipTrigger render={<span className="cursor-help text-sm">{relativeTimestamp}</span>} />
            <TooltipContent>{absoluteTimestamp}</TooltipContent>
          </Tooltip>
        </TableCell>
        <TableCell>
          <div className="flex items-center gap-2">
            <Avatar className="size-7">
              <AvatarFallback>{actorInitial}</AvatarFallback>
            </Avatar>
            <span className="text-sm">{entry.actorEmail}</span>
          </div>
        </TableCell>
        <TableCell>
          <Badge variant="secondary">{entry.action}</Badge>
        </TableCell>
        <TableCell className="text-sm text-muted-foreground">{resourceLabel}</TableCell>
        <TableCell className="text-sm text-muted-foreground">{entry.ipAddress ?? "—"}</TableCell>
      </TableRow>
      {isExpanded && (
        <TableRow>
          <TableCell colSpan={6} className="bg-muted/30">
            <AuditLogMetadata entry={entry} />
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

function AuditLogMetadata({ entry }: Readonly<{ entry: AuditLogEntry }>) {
  const prettyMetadata = formatMetadata(entry.metadata);

  return (
    <div className="grid gap-3 p-3 text-sm">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <MetadataField label={t`Entry ID`} value={entry.id} />
        <MetadataField label={t`Resource ID`} value={entry.resourceId} />
        <MetadataField label={t`Actor user ID`} value={entry.actorUserId} />
        <MetadataField label={t`User agent`} value={entry.userAgent} />
      </div>
      <div className="grid gap-1">
        <span className="text-xs font-medium text-muted-foreground">
          <Trans>Metadata</Trans>
        </span>
        <pre className="max-h-64 overflow-auto rounded-md border bg-background p-3 text-xs whitespace-pre-wrap">
          {prettyMetadata ?? "—"}
        </pre>
      </div>
    </div>
  );
}

function MetadataField({ label, value }: Readonly<{ label: string; value: string | null }>) {
  return (
    <div className="grid gap-0.5">
      <span className="text-xs font-medium text-muted-foreground">{label}</span>
      <span className="font-mono text-xs break-all">{value ?? "—"}</span>
    </div>
  );
}

function formatMetadata(metadata: string | null): string | null {
  if (!metadata) {
    return null;
  }
  try {
    return JSON.stringify(JSON.parse(metadata), null, 2);
  } catch {
    return metadata;
  }
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";

export function AuditLogTableSkeleton() {
  return (
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
          {Array.from({ length: 8 }).map((_, index) => (
            // biome-ignore lint/suspicious/noArrayIndexKey: skeleton rows have no stable identity
            <TableRow key={index}>
              <TableCell>
                <Skeleton className="size-6" />
              </TableCell>
              <TableCell>
                <Skeleton className="h-4 w-24" />
              </TableCell>
              <TableCell>
                <div className="flex items-center gap-2">
                  <Skeleton className="size-7 rounded-full" />
                  <Skeleton className="h-4 w-40" />
                </div>
              </TableCell>
              <TableCell>
                <Skeleton className="h-5 w-20" />
              </TableCell>
              <TableCell>
                <Skeleton className="h-4 w-32" />
              </TableCell>
              <TableCell>
                <Skeleton className="h-4 w-24" />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

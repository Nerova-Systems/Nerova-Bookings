import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";

import { ImportRowStatus, type Schemas } from "@/shared/lib/api/client";

type ImportRow = Schemas["ImportRowResponse"];

export function ImportRowsTable({ rows }: Readonly<{ rows: ImportRow[] }>) {
  return (
    <div className="overflow-hidden rounded-lg border">
      <Table rowSize="spacious" aria-label={t`Import rows`}>
        <TableHeader>
          <TableRow>
            <TableHead>
              <Trans>First name</Trans>
            </TableHead>
            <TableHead>
              <Trans>Last name</Trans>
            </TableHead>
            <TableHead>
              <Trans>Email</Trans>
            </TableHead>
            <TableHead>
              <Trans>Phone</Trans>
            </TableHead>
            <TableHead>
              <Trans>Status</Trans>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => (
            <TableRow
              key={row.rowNumber}
              rowKey={row.rowNumber}
              className={row.status === ImportRowStatus.Invalid ? "bg-destructive/5" : undefined}
            >
              <TableCell>{row.firstName}</TableCell>
              <TableCell>{row.lastName}</TableCell>
              <TableCell>{row.email ?? ""}</TableCell>
              <TableCell>{row.phoneNumber ?? ""}</TableCell>
              <TableCell>
                <div className="flex flex-col gap-1">
                  <ImportRowStatusBadge status={row.status} />
                  {row.status === ImportRowStatus.Invalid && row.error ? (
                    <span className="text-xs text-destructive">{row.error}</span>
                  ) : null}
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function ImportRowStatusBadge({ status }: Readonly<{ status: ImportRowStatus }>) {
  if (status === ImportRowStatus.Valid) {
    return (
      <Badge>
        <Trans>Ready</Trans>
      </Badge>
    );
  }
  if (status === ImportRowStatus.Duplicate) {
    return (
      <Badge variant="secondary">
        <Trans>Already exists</Trans>
      </Badge>
    );
  }
  return (
    <Badge variant="destructive">
      <Trans>Needs attention</Trans>
    </Badge>
  );
}

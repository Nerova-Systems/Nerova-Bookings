import { Trans } from "@lingui/react/macro";
import { TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";

export function BookingsTableHeader() {
  return (
    <TableHeader>
      <TableRow>
        <TableHead className="w-10" />
        <TableHead>
          <Trans>Client</Trans>
        </TableHead>
        <TableHead>
          <Trans>Service</Trans>
        </TableHead>
        <TableHead className="w-44">
          <Trans>Time</Trans>
        </TableHead>
        <TableHead className="w-36">
          <Trans>Status</Trans>
        </TableHead>
        <TableHead className="w-10" />
      </TableRow>
    </TableHeader>
  );
}

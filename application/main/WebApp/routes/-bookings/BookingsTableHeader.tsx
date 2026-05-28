import { Trans } from "@lingui/react/macro";
import { TableHead, TableHeader, TableRow } from "@repo/ui/components/Table";

export function BookingsTableHeader() {
  return (
    <TableHeader>
      <TableRow>
        <TableHead className="w-10" />
        <TableHead className="w-28">
          <Trans>Status</Trans>
        </TableHead>
        <TableHead>
          <Trans>Event type</Trans>
        </TableHead>
        <TableHead>
          <Trans>Attendee</Trans>
        </TableHead>
        <TableHead className="w-44">
          <Trans>Start time</Trans>
        </TableHead>
        <TableHead className="w-24">
          <Trans>Duration</Trans>
        </TableHead>
        <TableHead className="w-48">
          <Trans>Location</Trans>
        </TableHead>
        <TableHead className="w-24">
          <Trans>Rating</Trans>
        </TableHead>
        <TableHead className="w-10" />
      </TableRow>
    </TableHeader>
  );
}

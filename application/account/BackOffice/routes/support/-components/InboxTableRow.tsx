import { Avatar, AvatarFallback } from "@repo/ui/components/Avatar";
import { TableCell, TableRow } from "@repo/ui/components/Table";
import { MailIcon } from "lucide-react";

import type { Schemas } from "@/shared/lib/api/client";

import { SmartDateTime } from "@/shared/components/SmartDateTime";
import { getSubscriptionPlanLabel } from "@/shared/lib/api/labels";

import { CategoryPill } from "./CategoryPill";
import { getInitials } from "./displayName";
import { StatusPill } from "./StatusPill";

type Ticket = Schemas["AllTicketsSummary"];

export function InboxTableRow({ ticket }: Readonly<{ ticket: Ticket }>) {
  const reporterName = ticket.reporterName ?? ticket.reporterEmail.split("@")[0];
  return (
    <TableRow rowKey={ticket.id}>
      <TableCell>
        <Avatar size="default" className="size-8">
          <AvatarFallback className={ticket.assignee ? "bg-primary/10 text-primary" : undefined}>
            {ticket.assignee ? getInitials(ticket.assignee.displayName) : "?"}
          </AvatarFallback>
        </Avatar>
      </TableCell>
      <TableCell>
        <div className="flex min-w-0 flex-col gap-1">
          <span
            className={`truncate ${ticket.isUnreadForStaff ? "font-semibold text-foreground" : "font-medium text-foreground"}`}
          >
            {ticket.subject}
          </span>
          <div className="flex items-center gap-2">
            <CategoryPill category={ticket.category} />
            <span className="font-mono text-xs text-muted-foreground">#{ticket.shortDisplayId}</span>
          </div>
        </div>
      </TableCell>
      <TableCell className="hidden md:table-cell">
        <div className="flex min-w-0 items-center gap-2">
          <Avatar size="default" className="size-8">
            <AvatarFallback>{getInitials(reporterName)}</AvatarFallback>
          </Avatar>
          <div className="flex min-w-0 flex-col">
            <span className="truncate text-sm font-medium">
              {reporterName}
              <span className="ml-1 text-xs font-normal text-muted-foreground">· {ticket.reporterRoleSnapshot}</span>
            </span>
            <span className="flex min-w-0 items-center gap-1 text-xs text-muted-foreground">
              <MailIcon className="size-3 shrink-0" aria-hidden={true} />
              <span className="truncate">{ticket.reporterEmail}</span>
            </span>
          </div>
        </div>
      </TableCell>
      <TableCell className="hidden lg:table-cell">
        <div className="flex min-w-0 flex-col">
          <span className="truncate text-sm font-medium">{ticket.tenantName}</span>
          <span className="text-xs text-muted-foreground">{getSubscriptionPlanLabel(ticket.tenantPlan)}</span>
        </div>
      </TableCell>
      <TableCell>
        <StatusPill status={ticket.status} />
      </TableCell>
      <TableCell className="hidden text-right text-xs text-muted-foreground xl:table-cell">
        <SmartDateTime date={ticket.createdAt} />
      </TableCell>
      <TableCell className="text-right text-xs text-muted-foreground">
        <SmartDateTime date={ticket.lastActivityAt} />
      </TableCell>
    </TableRow>
  );
}

import { plural } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Avatar, AvatarFallback } from "@repo/ui/components/Avatar";
import { Link } from "@tanstack/react-router";
import { ChevronRightIcon, MailIcon } from "lucide-react";

import type { Schemas } from "@/shared/lib/api/client";

import { getSubscriptionPlanLabel } from "@/shared/lib/api/labels";

import { getInitials } from "./displayName";

const rowClass =
  "flex w-full cursor-pointer items-center gap-3 rounded-md border border-border bg-card px-3 py-2 text-left outline-ring transition-colors hover:bg-muted focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 active:bg-accent";

export function ReporterRow({ reporter }: { reporter: Schemas["StaffTicketReporter"] }) {
  const displayName = `${reporter.firstName ?? ""} ${reporter.lastName ?? ""}`.trim() || reporter.email;
  return (
    <Link to="/users/$userId" params={{ userId: reporter.id }} className={rowClass}>
      <Avatar size="default" className="size-10">
        <AvatarFallback>{getInitials(displayName)}</AvatarFallback>
      </Avatar>
      <div className="flex min-w-0 flex-1 flex-col">
        <span className="truncate text-sm font-medium">{displayName}</span>
        <span className="truncate text-xs text-muted-foreground">
          {reporter.roleSnapshot} · {plural(reporter.tenantTicketCount, { one: "# ticket", other: "# tickets" })}
        </span>
        <span className="flex items-center gap-1 truncate text-xs text-muted-foreground">
          <MailIcon className="size-3 shrink-0" aria-hidden={true} />
          <span className="truncate">{reporter.email}</span>
        </span>
      </div>
      <ChevronRightIcon className="size-4 text-muted-foreground" aria-hidden={true} />
    </Link>
  );
}

export function AccountRow({ account }: { account: Schemas["StaffTicketAccount"] }) {
  return (
    <Link to="/accounts/$tenantId" params={{ tenantId: account.id }} className={rowClass}>
      <Avatar size="default" className="size-10">
        <AvatarFallback>{getInitials(account.name)}</AvatarFallback>
      </Avatar>
      <div className="flex min-w-0 flex-1 flex-col">
        <span className="truncate text-sm font-medium">{account.name}</span>
        <span className="truncate text-xs text-muted-foreground">
          <Trans>Plan</Trans> · {getSubscriptionPlanLabel(account.plan)}
        </span>
        {/* TODO: CLV pending pricing system */}
      </div>
      <ChevronRightIcon className="size-4 text-muted-foreground" aria-hidden={true} />
    </Link>
  );
}

import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@repo/ui/components/Empty";
import { ShieldCheckIcon } from "lucide-react";

import type { App } from "./appsTypes";

/**
 * Renders the real OAuth scopes an app requests, mirroring cal.com's AppSettings/permissions list:
 * each permission shows its title as a bold label and its description as the explanation. The data is
 * always sourced from `app.permissions` (never hardcoded on the frontend).
 */
export function AppPermissionsList({ app, className }: Readonly<{ app: App; className?: string }>) {
  if (app.permissions.length === 0) {
    return (
      <Empty className={className}>
        <EmptyHeader>
          <EmptyTitle>
            <Trans>No special permissions</Trans>
          </EmptyTitle>
          <EmptyDescription>
            <Trans>This app does not request access to any of your data.</Trans>
          </EmptyDescription>
        </EmptyHeader>
      </Empty>
    );
  }

  return (
    <ul className={className}>
      {app.permissions.map((permission) => (
        <li key={permission.scope} className="flex items-start gap-3">
          <ShieldCheckIcon className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
          <div className="min-w-0">
            <p className="text-sm font-medium text-foreground">{permission.title}</p>
            <p className="mt-0.5 text-sm text-muted-foreground">{permission.description}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}

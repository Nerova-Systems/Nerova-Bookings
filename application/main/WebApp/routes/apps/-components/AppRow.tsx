import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@repo/ui/components/Collapsible";
import { Link as RouterLink } from "@tanstack/react-router";
import { ChevronDownIcon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import type { App } from "./appsTypes";

import { AppPermissionsList } from "./AppPermissionsList";
import { getAppCategoryLabel, getAppIconSrc } from "./appsTypes";

interface AppRowProps {
  app: App;
  onUninstall: (app: App) => void;
}

/**
 * Installed-app list item. Mirrors cal.com's AppList card: an app header row that expands (toggle) to
 * reveal the per-app permissions screen, rendered from the app's real OAuth scopes. Shows the user's
 * connection state and the Uninstall action.
 */
export function AppRow({ app, onUninstall }: Readonly<AppRowProps>) {
  const [expanded, setExpanded] = useState<boolean>(false);

  const installMutation = api.useMutation("post", "/api/apps/{slug}/install", {
    onSuccess: (response) => {
      window.location.href = response.authorizeUrl;
    },
    onError: (error) => {
      const detail = (error as { detail?: string } | null | undefined)?.detail;
      toast.error(detail ?? t`Failed to start install. Please try again.`);
    }
  });

  const isConnected = app.isConnectedForUser;

  return (
    <Collapsible open={expanded} onOpenChange={setExpanded} render={<li />}>
      <div className="flex flex-wrap items-start gap-x-3 gap-y-2 px-4 py-4 sm:px-6">
        <img
          src={getAppIconSrc(app)}
          alt=""
          className="size-10 shrink-0 rounded-md border bg-background object-contain p-1"
        />
        <div className="flex min-w-0 grow flex-col gap-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate text-sm font-semibold text-foreground">{app.name}</h3>
            <Badge variant="outline" className="text-[10px] font-semibold text-muted-foreground uppercase">
              {getAppCategoryLabel(app.category)}
            </Badge>
          </div>
          {app.description && <p className="line-clamp-2 text-sm text-muted-foreground">{app.description}</p>}
          {isConnected ? (
            <span className="mt-0.5 inline-flex items-center gap-1.5 text-xs font-medium text-emerald-600 dark:text-emerald-400">
              <span className="size-2 rounded-full bg-emerald-500" />
              <Trans>Connected</Trans>
            </span>
          ) : (
            <span className="mt-0.5 inline-flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
              <span className="size-2 rounded-full bg-muted-foreground/50" />
              <Trans>Not connected</Trans>
            </span>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            className="hidden sm:inline-flex"
            render={<RouterLink to="/apps/$slug" params={{ slug: app.slug }} />}
          >
            <Trans>Details</Trans>
          </Button>
          <CollapsibleTrigger
            render={
              <Button variant="ghost" size="sm" aria-label={t`Show permissions for ${app.name}`}>
                <Trans>Permissions</Trans>
                <ChevronDownIcon className={`size-4 transition-transform ${expanded ? "rotate-180" : ""}`} />
              </Button>
            }
          />
          {isConnected ? (
            <Button variant="outline" size="sm" onClick={() => onUninstall(app)}>
              <Trans>Uninstall</Trans>
            </Button>
          ) : (
            <Button
              size="sm"
              disabled={!app.isActive}
              isPending={installMutation.isPending}
              onClick={() => installMutation.mutate({ params: { path: { slug: app.slug } } })}
            >
              <Trans>Connect</Trans>
            </Button>
          )}
        </div>
      </div>

      <CollapsibleContent>
        <div className="border-t border-border px-4 py-5 sm:px-6">
          <h4 className="mb-3 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
            <Trans>Permissions</Trans>
          </h4>
          <AppPermissionsList app={app} className="flex flex-col gap-4" />
        </div>
      </CollapsibleContent>
    </Collapsible>
  );
}

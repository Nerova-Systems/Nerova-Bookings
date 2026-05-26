import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import type { App } from "./appsTypes";

import { getAppCategoryLabel, getMissingPrerequisite } from "./appsTypes";

interface AppRowProps {
  app: App;
  allApps: readonly App[];
  onUninstall: (app: App) => void;
}

export function AppRow({ app, allApps, onUninstall }: Readonly<AppRowProps>) {
  const installMutation = api.useMutation("post", "/api/apps/{slug}/install", {
    onSuccess: (response) => {
      window.location.href = response.authorizeUrl;
    },
    onError: (error) => {
      const detail = (error as { detail?: string } | null | undefined)?.detail;
      toast.error(detail ?? t`Failed to start install. Please try again.`);
    }
  });

  const missingPrerequisite = getMissingPrerequisite(app, allApps);
  const isInstalled = app.isConnectedForUser;
  const canInstall = app.isActive && missingPrerequisite === null && !isInstalled;

  return (
    <div className="flex flex-wrap items-start gap-4 border-b p-4 last:border-b-0 hover:bg-muted/60">
      <AppIcon app={app} />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="font-medium">{app.name}</span>
          <Badge variant="outline">{getAppCategoryLabel(app.category)}</Badge>
          {isInstalled ? (
            <Badge>
              <Trans>Installed</Trans>
            </Badge>
          ) : (
            <Badge variant="secondary">
              <Trans>Not installed</Trans>
            </Badge>
          )}
        </div>
        {app.description && <p className="mt-1 text-sm text-muted-foreground">{app.description}</p>}
        {missingPrerequisite && !isInstalled && (
          <p className="mt-2 text-sm text-amber-700 dark:text-amber-400">
            <Trans>Install {missingPrerequisite.name} first — this app reuses its connection.</Trans>
          </p>
        )}
      </div>
      <div className="flex shrink-0 gap-2">
        {isInstalled ? (
          <Button variant="outline" size="sm" onClick={() => onUninstall(app)}>
            <Trans>Uninstall</Trans>
          </Button>
        ) : (
          <Button
            size="sm"
            disabled={!canInstall}
            isPending={installMutation.isPending}
            onClick={() => installMutation.mutate({ params: { path: { slug: app.slug } } })}
          >
            <Trans>Install</Trans>
          </Button>
        )}
      </div>
    </div>
  );
}

function AppIcon({ app }: Readonly<{ app: App }>) {
  if (app.logoUrl) {
    return (
      <img src={app.logoUrl} alt="" className="h-10 w-10 shrink-0 rounded-md border bg-background object-contain p-1" />
    );
  }
  return (
    <div
      aria-hidden="true"
      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-md border bg-muted text-sm font-semibold text-muted-foreground"
    >
      {app.name.slice(0, 1).toUpperCase()}
    </div>
  );
}

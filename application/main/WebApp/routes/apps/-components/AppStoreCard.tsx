import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { CheckCircle2Icon, PlusIcon } from "lucide-react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import type { App } from "./appsTypes";

import { getAppCategoryLabel, getAppIconSrc, getMissingPrerequisite } from "./appsTypes";

interface AppStoreCardProps {
  app: App;
  allApps: readonly App[];
  onDetails: () => void;
}

/**
 * App Store gallery card. Layout mirrors cal.com `AppCard`: a fixed-height card with the logo, name,
 * a clamped multi-line description, and a bottom action row with a "Details" button plus the primary
 * Install/Connect action. An "Installed" badge sits in the top-right corner when connected.
 */
export function AppStoreCard({ app, allApps, onDetails }: Readonly<AppStoreCardProps>) {
  const installMutation = api.useMutation("post", "/api/apps/{slug}/install", {
    onSuccess: (response) => {
      window.location.href = response.authorizeUrl;
    },
    onError: (error) => {
      const detail = (error as { detail?: string } | null | undefined)?.detail;
      toast.error(detail ?? t`Failed to start install. Please try again.`);
    }
  });

  const isInstalled = app.isConnectedForUser;
  const missingPrerequisite = getMissingPrerequisite(app, allApps);
  const canInstall = app.isActive && missingPrerequisite === null && !isInstalled;

  return (
    <div className="relative flex h-64 flex-col rounded-md border border-border bg-card p-5">
      <div className="absolute top-4 right-4 flex flex-wrap justify-end gap-1">
        {isInstalled && (
          <Badge className="flex items-center gap-1 border-emerald-500/20 bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
            <CheckCircle2Icon className="size-3" />
            <Trans>Installed</Trans>
          </Badge>
        )}
      </div>

      <img
        src={getAppIconSrc(app)}
        alt=""
        className="mb-4 size-12 rounded-sm border bg-background object-contain p-1"
      />

      <div className="flex items-center gap-2">
        <h3 className="font-medium text-foreground">{app.name}</h3>
      </div>
      <Badge
        variant="outline"
        className="mt-2 text-[10px] font-semibold tracking-wider text-muted-foreground uppercase"
      >
        {getAppCategoryLabel(app.category)}
      </Badge>

      <p className="mt-2 line-clamp-3 text-sm text-muted-foreground">{app.description}</p>

      <div className="mt-auto flex max-w-full flex-row justify-between gap-2">
        <Button variant="secondary" size="sm" className="grow justify-center" onClick={onDetails}>
          <Trans>Details</Trans>
        </Button>
        {isInstalled ? (
          <Button variant="outline" size="sm" disabled className="justify-center">
            <CheckCircle2Icon className="size-4" />
            <Trans>Connected</Trans>
          </Button>
        ) : (
          <Button
            size="sm"
            className="justify-center"
            disabled={!canInstall}
            isPending={installMutation.isPending}
            onClick={() => installMutation.mutate({ params: { path: { slug: app.slug } } })}
          >
            <PlusIcon className="size-4" />
            <Trans>Install</Trans>
          </Button>
        )}
      </div>
    </div>
  );
}

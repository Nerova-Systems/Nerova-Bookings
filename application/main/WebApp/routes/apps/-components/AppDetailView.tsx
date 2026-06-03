import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import {
  AlertTriangleIcon,
  ArrowRightIcon,
  CheckCircle2Icon,
  ExternalLinkIcon,
  MailIcon,
  Trash2Icon
} from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import type { App } from "./appsTypes";

import { AppPermissionsList } from "./AppPermissionsList";
import { AppScreenshotCarousel } from "./AppScreenshotCarousel";
import { getAppCategoryLabel, getAppIconSrc, getMissingPrerequisite } from "./appsTypes";
import { UninstallAppDialog } from "./UninstallAppDialog";

export function AppDetailView({ app, allApps }: Readonly<{ app: App; allApps: readonly App[] }>) {
  const [appToUninstall, setAppToUninstall] = useState<App | null>(null);

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
    <div className="flex flex-col gap-8">
      <div className="flex flex-col gap-4 border-b border-border pb-6 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-4">
          <img
            src={getAppIconSrc(app)}
            alt=""
            className="size-16 shrink-0 rounded-xl border bg-background object-contain p-2 shadow-sm"
          />
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold text-foreground">{app.name}</h1>
              {isInstalled && (
                <Badge className="flex items-center gap-1 border-emerald-500/20 bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                  <CheckCircle2Icon className="size-3" />
                  <Trans>Connected</Trans>
                </Badge>
              )}
            </div>
            <p className="mt-1 text-sm text-muted-foreground">
              {app.publisher ? <Trans>By {app.publisher}</Trans> : null}
              {app.publisher ? " · " : null}
              {getAppCategoryLabel(app.category)}
            </p>
          </div>
        </div>
        <div className="shrink-0">
          {isInstalled ? (
            <Button variant="destructive" size="sm" onClick={() => setAppToUninstall(app)}>
              <Trash2Icon className="size-4" />
              <Trans>Uninstall app</Trans>
            </Button>
          ) : (
            <Button
              size="sm"
              disabled={!canInstall}
              isPending={installMutation.isPending}
              onClick={() => installMutation.mutate({ params: { path: { slug: app.slug } } })}
            >
              <Trans>Install app</Trans>
              <ArrowRightIcon className="size-4" />
            </Button>
          )}
        </div>
      </div>

      <AppScreenshotCarousel screenshots={app.screenshots} appName={app.name} />

      {missingPrerequisite && (
        <div className="flex gap-3 rounded-lg border border-amber-200 bg-amber-50/50 p-4 dark:border-amber-900/50 dark:bg-amber-950/20">
          <AlertTriangleIcon className="mt-0.5 size-4 shrink-0 text-amber-600 dark:text-amber-400" />
          <div className="text-sm">
            <p className="font-semibold text-amber-800 dark:text-amber-300">
              <Trans>Prerequisite required</Trans>
            </p>
            <p className="mt-0.5 text-amber-700/95 dark:text-amber-400/90">
              <Trans>
                You must install and connect <strong>{missingPrerequisite.name}</strong> first before you can enable{" "}
                {app.name}.
              </Trans>
            </p>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-[1fr_18rem]">
        <div className="flex flex-col gap-8">
          <section>
            <h2 className="mb-2 text-sm font-semibold tracking-wider text-muted-foreground uppercase">
              <Trans>Overview</Trans>
            </h2>
            <p className="text-sm leading-relaxed whitespace-pre-line text-foreground/90">{app.overview}</p>
          </section>

          <section>
            <h2 className="mb-3 text-sm font-semibold tracking-wider text-muted-foreground uppercase">
              <Trans>Permissions</Trans>
            </h2>
            <AppPermissionsList app={app} className="flex flex-col gap-4" />
          </section>
        </div>

        <AppDetailSidebar app={app} />
      </div>

      <UninstallAppDialog
        app={appToUninstall}
        isOpen={appToUninstall !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) setAppToUninstall(null);
        }}
      />
    </div>
  );
}

function AppDetailSidebar({ app }: Readonly<{ app: App }>) {
  return (
    <aside className="flex flex-col gap-6 lg:border-l lg:border-border lg:pl-8">
      <div>
        <h3 className="mb-1 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
          <Trans>Pricing</Trans>
        </h3>
        <p className="text-sm font-medium text-foreground">{app.pricing}</p>
      </div>
      {app.publisher && (
        <div>
          <h3 className="mb-1 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
            <Trans>Publisher</Trans>
          </h3>
          <p className="text-sm text-foreground">{app.publisher}</p>
        </div>
      )}
      <div>
        <h3 className="mb-1 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
          <Trans>Category</Trans>
        </h3>
        <p className="text-sm text-foreground">{getAppCategoryLabel(app.category)}</p>
      </div>
      {(app.website || app.supportEmail) && (
        <div>
          <h3 className="mb-2 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
            <Trans>Contact</Trans>
          </h3>
          <div className="flex flex-col gap-2">
            {app.website && (
              <a
                href={app.website}
                target="_blank"
                rel="noreferrer noopener"
                className="flex items-center gap-2 text-sm text-primary outline-ring hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <ExternalLinkIcon className="size-4 shrink-0" />
                <Trans>Website</Trans>
              </a>
            )}
            {app.supportEmail && (
              <a
                href={`mailto:${app.supportEmail}`}
                className="flex items-center gap-2 text-sm text-primary outline-ring hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2"
              >
                <MailIcon className="size-4 shrink-0" />
                <Trans>Contact support</Trans>
              </a>
            )}
          </div>
        </div>
      )}
    </aside>
  );
}

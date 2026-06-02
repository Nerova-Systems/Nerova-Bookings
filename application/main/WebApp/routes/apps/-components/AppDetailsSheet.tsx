import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Sheet, SheetContent, SheetDescription, SheetTitle } from "@repo/ui/components/Sheet";
import { AlertTriangleIcon, ArrowRightIcon, CheckCircle2Icon, Trash2Icon } from "lucide-react";
import { toast } from "sonner";

import { api } from "@/shared/lib/api/client";

import type { App } from "./appsTypes";

import { AppPermissionsList } from "./AppPermissionsList";
import { getAppCategoryLabel, getAppIconSrc, getMissingPrerequisite } from "./appsTypes";

interface AppDetailsSheetProps {
  app: App | null;
  allApps: readonly App[];
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  onUninstall: (app: App) => void;
}

/**
 * Slide-out detail surface for an app. Shows the real full description, category, connection state,
 * and the app's real OAuth permissions (rendered from `app.permissions`). Mirrors cal.com's app
 * detail view: an overview plus the truthful per-app permissions list.
 */
export function AppDetailsSheet({ app, allApps, isOpen, onOpenChange, onUninstall }: Readonly<AppDetailsSheetProps>) {
  const installMutation = api.useMutation("post", "/api/apps/{slug}/install", {
    onSuccess: (response) => {
      window.location.href = response.authorizeUrl;
    },
    onError: (error) => {
      const detail = (error as { detail?: string } | null | undefined)?.detail;
      toast.error(detail ?? t`Failed to start install. Please try again.`);
    }
  });

  if (!app) return null;

  const isInstalled = app.isConnectedForUser;
  const missingPrerequisite = getMissingPrerequisite(app, allApps);
  const canInstall = app.isActive && missingPrerequisite === null && !isInstalled;

  return (
    <Sheet open={isOpen} onOpenChange={onOpenChange}>
      <SheetContent side="right" className="w-[32rem] border-l border-border bg-popover p-0 sm:max-w-xl">
        <div className="flex flex-1 flex-col gap-6 overflow-y-auto p-6">
          {/* Header */}
          <div className="flex items-start gap-4 border-b border-border pb-5">
            <img
              src={getAppIconSrc(app)}
              alt=""
              className="size-12 shrink-0 rounded-xl border bg-background object-contain p-2 shadow-sm"
            />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <SheetTitle className="text-xl leading-tight font-bold text-foreground">{app.name}</SheetTitle>
                <Badge variant="outline" className="text-[10px] font-semibold text-muted-foreground uppercase">
                  {getAppCategoryLabel(app.category)}
                </Badge>
                {isInstalled && (
                  <Badge className="flex items-center gap-1 border-emerald-500/20 bg-emerald-500/10 text-[10px] font-semibold text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                    <CheckCircle2Icon className="size-3" />
                    <Trans>Connected</Trans>
                  </Badge>
                )}
              </div>
              <SheetDescription className="mt-1 text-xs text-muted-foreground">{app.description}</SheetDescription>
            </div>
          </div>

          <div className="flex flex-1 flex-col gap-6">
            {/* About */}
            <section>
              <h4 className="mb-2 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                <Trans>About</Trans>
              </h4>
              <p className="text-sm leading-relaxed text-muted-foreground">{app.description}</p>
            </section>

            {/* Real permissions / scopes */}
            <section>
              <h4 className="mb-3 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                <Trans>Permissions</Trans>
              </h4>
              <AppPermissionsList app={app} className="flex flex-col gap-4" />
            </section>

            {missingPrerequisite && (
              <div className="flex gap-3 rounded-lg border border-amber-200 bg-amber-50/50 p-4 dark:border-amber-900/50 dark:bg-amber-950/20">
                <AlertTriangleIcon className="mt-0.5 size-4 shrink-0 text-amber-600 dark:text-amber-400" />
                <div className="text-xs">
                  <p className="font-semibold text-amber-800 dark:text-amber-300">
                    <Trans>Prerequisite required</Trans>
                  </p>
                  <p className="mt-0.5 text-amber-700/95 dark:text-amber-400/90">
                    <Trans>
                      You must install and connect <strong>{missingPrerequisite.name}</strong> first before you can
                      enable {app.name}.
                    </Trans>
                  </p>
                </div>
              </div>
            )}
          </div>

          {/* Action buttons */}
          <div className="mt-auto flex items-center justify-end border-t border-border pt-5">
            {isInstalled ? (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => {
                  onOpenChange(false);
                  onUninstall(app);
                }}
              >
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
      </SheetContent>
    </Sheet>
  );
}

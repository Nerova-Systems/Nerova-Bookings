import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { createFileRoute, Link as RouterLink, useNavigate } from "@tanstack/react-router";
import { BlocksIcon, Settings2Icon } from "lucide-react";
import { useEffect, useState } from "react";

import { api } from "@/shared/lib/api/client";

import type { App } from "./-components/appsTypes";

import { AppDetailsSheet } from "./-components/AppDetailsSheet";
import { AppsPageShell } from "./-components/AppsPageShell";
import { getAppCategoryLabel } from "./-components/appsTypes";
import { UninstallAppDialog } from "./-components/UninstallAppDialog";

interface AppsInstalledSearchQuery {
  open?: string;
}

export const Route = createFileRoute("/apps/installed")({
  staticData: { trackingTitle: "Connected Apps" },
  validateSearch: (search: Record<string, unknown>): AppsInstalledSearchQuery => {
    return {
      open: typeof search.open === "string" ? search.open : undefined
    };
  },
  component: InstalledAppsPage
});

function InstalledAppsPage() {
  const { open } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const { data, isLoading } = api.useQuery("get", "/api/apps");
  const apps = data?.apps ?? [];
  const installedApps = apps.filter(
    (app) => (app.slug === "whatsapp" ? app.isInstalledForTenant : app.isConnectedForUser)
  );

  const [selectedApp, setSelectedApp] = useState<App | null>(null);
  const [appToUninstall, setAppToUninstall] = useState<App | null>(null);

  // Sync state with open slug in query parameters
  useEffect(() => {
    if (open && apps.length > 0) {
      const matched = apps.find((a) => a.slug === open);
      if (matched) {
        setSelectedApp(matched);
      }
    } else {
      setSelectedApp(null);
    }
  }, [open, apps]);

  const handleOpenDetails = (app: App) => {
    setSelectedApp(app);
    void navigate({ search: { open: app.slug } });
  };

  const handleCloseDetails = () => {
    setSelectedApp(null);
    void navigate({ search: { open: undefined } });
  };

  return (
    <AppsPageShell
      title={t`Connected Apps`}
      subtitle={t`Configure your active integrations, calendar destinations, and business messaging accounts in one unified dashboard.`}
    >
      <section className="flex min-w-0 flex-col gap-6">
        {isLoading ? (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {[...Array(2)].map((_, i) => (
              <div
                key={`installed-shimmer-${i}`}
                className="h-[120px] animate-pulse rounded-xl border border-border bg-card/40 p-5"
              />
            ))}
          </div>
        ) : installedApps.length === 0 ? (
          <Empty className="min-h-64 rounded-2xl border bg-card/30">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <BlocksIcon className="size-8" />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No active integrations</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>You haven't connected any apps yet. Go to the App Store to search for extensions.</Trans>
              </EmptyDescription>
              <div className="mt-4">
                <Button render={<RouterLink to="/apps" />}>
                  <Trans>Explore App Store</Trans>
                </Button>
              </div>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            {installedApps.map((app) => (
              <div
                key={app.slug}
                onClick={() => handleOpenDetails(app)}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => {
                  if (e.key === "Enter" || e.key === " ") {
                    handleOpenDetails(app);
                  }
                }}
                className="group relative flex cursor-pointer flex-col justify-between overflow-hidden rounded-xl border border-border bg-card p-5 shadow-sm transition-all duration-300 hover:border-primary/20 hover:shadow-md"
              >
                <div className="flex items-start gap-4">
                  <AppIcon app={app} />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-semibold text-foreground transition-colors duration-200 group-hover:text-primary">
                        {app.name}
                      </span>
                      <Badge variant="outline" className="text-[10px] font-semibold text-muted-foreground uppercase">
                        {getAppCategoryLabel(app.category)}
                      </Badge>
                    </div>
                    {app.description && (
                      <p className="mt-1 line-clamp-2 text-xs text-muted-foreground">{app.description}</p>
                    )}
                  </div>
                </div>

                <div className="mt-5 flex items-center justify-between border-t pt-3">
                  <span className="inline-flex items-center gap-1.5 text-xs font-medium text-emerald-600 dark:text-emerald-400">
                    <span className="size-2 animate-pulse rounded-full bg-emerald-500" />
                    <Trans>Connected</Trans>
                  </span>
                  <Button
                    size="xs"
                    variant="outline"
                    className="flex items-center gap-1 transition-all duration-200 group-hover:border-primary group-hover:bg-primary group-hover:text-primary-foreground"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleOpenDetails(app);
                    }}
                  >
                    <Settings2Icon className="size-3" />
                    <Trans>Configure</Trans>
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Slide-out details drawer */}
      <AppDetailsSheet
        app={selectedApp}
        allApps={apps}
        isOpen={selectedApp !== null}
        onOpenChange={(open) => {
          if (!open) handleCloseDetails();
        }}
        onUninstall={setAppToUninstall}
      />

      {/* Platform uninstallation confirmation dialog */}
      <UninstallAppDialog
        app={appToUninstall}
        isOpen={appToUninstall !== null}
        onOpenChange={(open) => {
          if (!open) setAppToUninstall(null);
        }}
      />
    </AppsPageShell>
  );
}

function AppIcon({ app }: Readonly<{ app: App }>) {
  if (app.logoUrl) {
    return (
      <img
        src={app.logoUrl}
        alt=""
        className="h-10 w-10 shrink-0 rounded-xl border bg-background object-contain p-1.5 shadow-sm"
      />
    );
  }
  return (
    <div
      aria-hidden="true"
      className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border bg-muted text-base font-bold text-muted-foreground shadow-sm"
    >
      {app.name.slice(0, 1).toUpperCase()}
    </div>
  );
}

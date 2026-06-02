import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { HorizontalTabs } from "@repo/ui/components/HorizontalTabs";
import { VerticalTabs, type VerticalTabItem } from "@repo/ui/components/VerticalTabs";
import { createFileRoute, Link as RouterLink } from "@tanstack/react-router";
import { BlocksIcon, CalendarIcon, CreditCardIcon, Grid3x3Icon, LayoutGridIcon, VideoIcon } from "lucide-react";
import { useMemo, useState } from "react";

import { AppCategory, api } from "@/shared/lib/api/client";

import type { App } from "./-components/appsTypes";

import { AppRow } from "./-components/AppRow";
import { AppsPageShell } from "./-components/AppsPageShell";
import { APP_CATEGORY_ORDER, getAppCategoryLabel, getVisibleApps } from "./-components/appsTypes";
import { UninstallAppDialog } from "./-components/UninstallAppDialog";

export const Route = createFileRoute("/apps/installed")({
  staticData: { trackingTitle: "Connected Apps" },
  // `open` is accepted for backwards compatibility with deep links (e.g. the /whatsapp redirect).
  // WhatsApp now lives in its own section, so it is filtered out and the param is otherwise unused.
  validateSearch: (search: Record<string, unknown>): { open?: string } => ({
    open: typeof search.open === "string" ? search.open : undefined
  }),
  component: InstalledAppsPage
});

const ALL_TAB = "all";

const CATEGORY_ICON: Readonly<Record<AppCategory, React.ReactNode>> = {
  [AppCategory.Calendar]: <CalendarIcon className="size-4" />,
  [AppCategory.Conferencing]: <VideoIcon className="size-4" />,
  [AppCategory.Payment]: <CreditCardIcon className="size-4" />,
  [AppCategory.Other]: <Grid3x3Icon className="size-4" />
};

function InstalledAppsPage() {
  const { data, isLoading } = api.useQuery("get", "/api/apps");
  const installedApps = useMemo(
    () => getVisibleApps(data?.apps ?? []).filter((app) => app.isInstalledForTenant),
    [data?.apps]
  );

  const [appToUninstall, setAppToUninstall] = useState<App | null>(null);
  const [selectedTab, setSelectedTab] = useState<string>(ALL_TAB);

  // Only show category tabs that actually have installed apps.
  const availableCategories = useMemo(
    () => APP_CATEGORY_ORDER.filter((category) => installedApps.some((app) => app.category === category)),
    [installedApps]
  );

  const tabs: VerticalTabItem[] = useMemo(
    () => [
      { value: ALL_TAB, label: t`All apps`, icon: <LayoutGridIcon className="size-4" /> },
      ...availableCategories.map((category) => ({
        value: category,
        label: getAppCategoryLabel(category),
        icon: CATEGORY_ICON[category]
      }))
    ],
    [availableCategories]
  );

  // Keep the active tab valid as the installed set changes.
  const activeTab =
    selectedTab !== ALL_TAB && !availableCategories.includes(selectedTab as AppCategory) ? ALL_TAB : selectedTab;

  const visibleApps = activeTab === ALL_TAB ? installedApps : installedApps.filter((app) => app.category === activeTab);

  return (
    <AppsPageShell
      title={t`Connected Apps`}
      subtitle={t`Configure your active integrations and calendar destinations in one unified dashboard.`}
    >
      {isLoading ? (
        <div className="grid grid-cols-1 gap-4">
          {[...Array(3)].map((_, i) => (
            <div
              key={`installed-shimmer-${i}`}
              className="h-20 animate-pulse rounded-md border border-border bg-card/40"
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
              <Trans>No installed apps</Trans>
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
        /* Category navigation: VerticalTabs on desktop (xl), HorizontalTabs on mobile (mirrors cal.com AppCategoryNavigation). */
        <div className="flex flex-col gap-6 xl:flex-row xl:gap-6">
          <div className="hidden xl:block xl:w-56 xl:shrink-0">
            <VerticalTabs tabs={tabs} value={activeTab} onValueChange={setSelectedTab} className="sticky top-6" />
          </div>
          <div className="block xl:hidden">
            <HorizontalTabs tabs={tabs} value={activeTab} onValueChange={setSelectedTab} />
          </div>

          <main className="min-w-0 flex-1">
            <ul className="divide-y divide-border overflow-hidden rounded-md border border-border bg-card">
              {visibleApps.map((app) => (
                <AppRow key={app.slug} app={app} onUninstall={setAppToUninstall} />
              ))}
            </ul>
          </main>
        </div>
      )}

      {/* Uninstall confirmation dialog */}
      <UninstallAppDialog
        app={appToUninstall}
        isOpen={appToUninstall !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) setAppToUninstall(null);
        }}
      />
    </AppsPageShell>
  );
}

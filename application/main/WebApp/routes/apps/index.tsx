import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Input } from "@repo/ui/components/Input";
import { cn } from "@repo/ui/utils";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { BlocksIcon, SearchIcon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import type { AppCategory } from "@/shared/lib/api/client";

import { api } from "@/shared/lib/api/client";

import type { App } from "./-components/appsTypes";

import { AppDetailsSheet } from "./-components/AppDetailsSheet";
import { AppsPageShell } from "./-components/AppsPageShell";
import { AppStoreCard } from "./-components/AppStoreCard";
import { APP_CATEGORY_ORDER, getAppCategoryLabel, getVisibleApps } from "./-components/appsTypes";
import { UninstallAppDialog } from "./-components/UninstallAppDialog";

interface AppsSearchQuery {
  open?: string;
}

export const Route = createFileRoute("/apps/")({
  staticData: { trackingTitle: "App Store" },
  validateSearch: (search: Record<string, unknown>): AppsSearchQuery => {
    return {
      open: typeof search.open === "string" ? search.open : undefined
    };
  },
  component: AppStoreGalleryPage
});

function AppStoreGalleryPage() {
  const { open } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const { data, isLoading } = api.useQuery("get", "/api/apps");
  const apps = useMemo(() => getVisibleApps(data?.apps ?? []), [data?.apps]);

  const [selectedApp, setSelectedApp] = useState<App | null>(null);
  const [appToUninstall, setAppToUninstall] = useState<App | null>(null);
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [selectedCategory, setSelectedCategory] = useState<AppCategory | null>(null);

  // Sync the open details drawer with the `open` slug in query parameters.
  useEffect(() => {
    if (open && apps.length > 0) {
      const matched = apps.find((a) => a.slug === open);
      setSelectedApp(matched ?? null);
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

  // Only show category tabs that actually have apps.
  const availableCategories = useMemo(
    () => APP_CATEGORY_ORDER.filter((category) => apps.some((app) => app.category === category)),
    [apps]
  );

  const normalizedSearch = searchQuery.trim().toLowerCase();
  const filteredApps = apps
    .filter((app) => (selectedCategory === null ? true : app.category === selectedCategory))
    .filter((app) =>
      normalizedSearch
        ? app.name.toLowerCase().includes(normalizedSearch) || app.description.toLowerCase().includes(normalizedSearch)
        : true
    )
    .sort((a, b) => a.name.localeCompare(b.name));

  return (
    <AppsPageShell
      title={t`App Store`}
      subtitle={t`Explore and connect powerful app extensions and calendar sync options to automate bookings.`}
    >
      <div className="flex flex-col gap-6">
        {/* Search */}
        <div className="relative max-w-md">
          <SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="search"
            placeholder={t`Search apps...`}
            className="pl-9"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>

        {/* Category pill tabs (mirrors cal.com AllApps CategoryTab) */}
        <div className="flex flex-col justify-between gap-3 lg:flex-row lg:items-center">
          <h2 className="text-base font-semibold text-foreground">
            {normalizedSearch ? (
              <Trans>Search</Trans>
            ) : selectedCategory === null ? (
              <Trans>All apps</Trans>
            ) : (
              getAppCategoryLabel(selectedCategory)
            )}
          </h2>
          <ul
            className="no-scrollbar flex max-w-full gap-1 overflow-x-auto"
            role="tablist"
            aria-label={t`App categories`}
          >
            <CategoryPill
              label={t`All`}
              isActive={selectedCategory === null}
              onClick={() => setSelectedCategory(null)}
            />
            {availableCategories.map((category) => (
              <CategoryPill
                key={category}
                label={getAppCategoryLabel(category)}
                isActive={selectedCategory === category}
                onClick={() => setSelectedCategory(selectedCategory === category ? null : category)}
              />
            ))}
          </ul>
        </div>

        {/* Gallery grid */}
        {isLoading ? (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {[...Array(8)].map((_, i) => (
              <div key={`shimmer-${i}`} className="h-64 animate-pulse rounded-md border border-border bg-card/40" />
            ))}
          </div>
        ) : filteredApps.length === 0 ? (
          <Empty className="min-h-64 rounded-2xl border bg-card/30">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <BlocksIcon className="size-8" />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No apps found</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Try adjusting your search criteria or select a different category.</Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {filteredApps.map((app) => (
              <AppStoreCard key={app.slug} app={app} allApps={apps} onDetails={() => handleOpenDetails(app)} />
            ))}
          </div>
        )}
      </div>

      {/* Slide-out details drawer */}
      <AppDetailsSheet
        app={selectedApp}
        allApps={apps}
        isOpen={selectedApp !== null}
        onOpenChange={(isOpen) => {
          if (!isOpen) handleCloseDetails();
        }}
        onUninstall={setAppToUninstall}
      />

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

function CategoryPill({
  label,
  isActive,
  onClick
}: Readonly<{ label: string; isActive: boolean; onClick: () => void }>) {
  return (
    <li>
      <button
        type="button"
        role="tab"
        aria-selected={isActive}
        onClick={onClick}
        className={cn(
          "min-w-max rounded-md px-4 py-2 text-sm font-medium whitespace-nowrap outline-ring transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
          isActive ? "bg-primary text-primary-foreground" : "bg-muted text-foreground hover:bg-muted/70"
        )}
      >
        {label}
      </button>
    </li>
  );
}

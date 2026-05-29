import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Input } from "@repo/ui/components/Input";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { BlocksIcon, SearchIcon } from "lucide-react";
import { useEffect, useState } from "react";

import { api } from "@/shared/lib/api/client";

import type { App } from "./-components/appsTypes";

import { AppDetailsSheet } from "./-components/AppDetailsSheet";
import { AppsPageShell } from "./-components/AppsPageShell";
import { AppStoreCard } from "./-components/AppStoreCard";
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
  const apps = data?.apps ?? [];

  const [selectedApp, setSelectedApp] = useState<App | null>(null);
  const [appToUninstall, setAppToUninstall] = useState<App | null>(null);
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [selectedCategory, setSelectedCategory] = useState<string>("all");

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

  const filteredApps = apps.filter((app) => {
    const matchesSearch =
      app.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      (app.description?.toLowerCase().includes(searchQuery.toLowerCase()) ?? false);

    const matchesCategory = selectedCategory === "all" || app.category.toLowerCase() === selectedCategory.toLowerCase();

    return matchesSearch && matchesCategory;
  });

  const categories = [
    { value: "all", label: t`All Apps` },
    { value: "calendar", label: t`Calendars` },
    { value: "conferencing", label: t`Conferencing` },
    { value: "payment", label: t`Payments` },
    { value: "other", label: t`Messaging & Other` }
  ];

  return (
    <AppsPageShell
      title={t`App Store`}
      subtitle={t`Explore and connect powerful app extensions, calendar sync options, and messaging workflows to automate bookings.`}
    >
      <div className="flex flex-col gap-6">
        {/* Search and Category filters */}
        <div className="flex flex-col items-stretch justify-between gap-4 border-b pb-6 md:flex-row md:items-center">
          <div className="relative max-w-md flex-1">
            <SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              type="search"
              placeholder={t`Search app integrations...`}
              className="h-10 w-full rounded-xl bg-muted/30 pl-9 transition-colors focus-visible:bg-background"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>

          <div className="no-scrollbar flex flex-wrap items-center gap-1.5 overflow-x-auto">
            {categories.map((cat) => (
              <button
                key={cat.value}
                type="button"
                onClick={() => setSelectedCategory(cat.value)}
                className={`rounded-lg border px-3 py-1.5 text-xs font-semibold transition-all duration-200 ${
                  selectedCategory === cat.value
                    ? "border-primary bg-primary text-primary-foreground shadow-sm shadow-primary/20"
                    : "border-border bg-card text-muted-foreground hover:bg-muted/50 hover:text-foreground"
                }`}
              >
                {cat.label}
              </button>
            ))}
          </div>
        </div>

        {/* Gallery Grid */}
        {isLoading ? (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {[...Array(6)].map((_, i) => (
              <div
                key={`shimmer-${i}`}
                className="flex h-[140px] animate-pulse flex-col justify-between rounded-xl border border-border bg-card/40 p-5"
              >
                <div className="flex items-start gap-4">
                  <div className="size-12 animate-pulse rounded-xl bg-muted" />
                  <div className="flex-1 space-y-2">
                    <div className="h-4 w-1/2 animate-pulse rounded bg-muted" />
                    <div className="h-3 w-3/4 animate-pulse rounded bg-muted" />
                  </div>
                </div>
                <div className="flex items-center justify-between border-t pt-3">
                  <div className="h-4 w-1/4 animate-pulse rounded bg-muted" />
                  <div className="h-4 w-1/4 animate-pulse rounded bg-muted" />
                </div>
              </div>
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
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {filteredApps.map((app) => (
              <AppStoreCard key={app.slug} app={app} onClick={() => handleOpenDetails(app)} />
            ))}
          </div>
        )}
      </div>

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

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Input } from "@repo/ui/components/Input";
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { BlocksIcon, SearchIcon } from "lucide-react";
import { useMemo, useState } from "react";

import type { AppCategory } from "@/shared/lib/api/client";

import { api } from "@/shared/lib/api/client";

import type { App } from "./-components/appsTypes";

import { AppSlider } from "./-components/AppSlider";
import { AppsPageShell } from "./-components/AppsPageShell";
import { AppStoreCard } from "./-components/AppStoreCard";
import { CategoryCard, CategoryPill } from "./-components/AppStoreFilters";
import { APP_CATEGORY_ORDER, getAppCategoryLabel, getVisibleApps } from "./-components/appsTypes";

export const Route = createFileRoute("/apps/")({
  staticData: { trackingTitle: "App Store" },
  component: AppStoreGalleryPage
});

/** Curated "most popular" order. Apps not listed fall after the curated ones, alphabetically. */
const POPULAR_ORDER: readonly string[] = ["google-calendar", "zoom", "google-meet", "office365-calendar", "ms-teams"];

function popularRank(slug: string): number {
  const index = POPULAR_ORDER.indexOf(slug);
  return index === -1 ? POPULAR_ORDER.length : index;
}

function AppStoreGalleryPage() {
  const navigate = useNavigate();
  const { data, isLoading } = api.useQuery("get", "/api/apps");
  const apps = useMemo(() => getVisibleApps(data?.apps ?? []), [data?.apps]);

  const [searchQuery, setSearchQuery] = useState<string>("");
  const [selectedCategory, setSelectedCategory] = useState<AppCategory | null>(null);

  const openDetails = (app: App) => {
    void navigate({ to: "/apps/$slug", params: { slug: app.slug } });
  };

  const availableCategories = useMemo(
    () => APP_CATEGORY_ORDER.filter((category) => apps.some((app) => app.category === category)),
    [apps]
  );

  const categoryCounts = useMemo(() => {
    const counts = new Map<AppCategory, number>();
    for (const app of apps) {
      counts.set(app.category, (counts.get(app.category) ?? 0) + 1);
    }
    return counts;
  }, [apps]);

  const popularApps = useMemo(
    () =>
      [...apps].sort((a, b) => popularRank(a.slug) - popularRank(b.slug) || a.name.localeCompare(b.name)).slice(0, 8),
    [apps]
  );

  const newApps = useMemo(() => apps.filter((app) => app.isNew).sort((a, b) => a.name.localeCompare(b.name)), [apps]);

  const normalizedSearch = searchQuery.trim().toLowerCase();
  const isFiltering = normalizedSearch.length > 0 || selectedCategory !== null;
  const filteredApps = apps
    .filter((app) => (selectedCategory === null ? true : app.category === selectedCategory))
    .filter((app) =>
      normalizedSearch
        ? app.name.toLowerCase().includes(normalizedSearch) || app.description.toLowerCase().includes(normalizedSearch)
        : true
    )
    .sort((a, b) => a.name.localeCompare(b.name));

  const searchInput = (
    <div className="relative w-full sm:w-72">
      <SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        type="search"
        placeholder={t`Search apps...`}
        className="pl-9"
        value={searchQuery}
        onChange={(e) => setSearchQuery(e.target.value)}
      />
    </div>
  );

  return (
    <AppsPageShell
      title={t`App Store`}
      subtitle={t`Explore and connect powerful app extensions and calendar sync options to automate bookings.`}
      actions={searchInput}
    >
      <div className="flex flex-col gap-10">
        {!isFiltering && !isLoading && availableCategories.length > 0 && (
          <AppSlider title={<Trans>Featured categories</Trans>}>
            {availableCategories.map((category) => (
              <div key={category} className="w-64 shrink-0 snap-start">
                <CategoryCard
                  category={category}
                  count={categoryCounts.get(category) ?? 0}
                  onClick={() => setSelectedCategory(category)}
                />
              </div>
            ))}
          </AppSlider>
        )}

        {!isFiltering && !isLoading && popularApps.length > 0 && (
          <AppSlider title={<Trans>Most popular</Trans>}>
            {popularApps.map((app) => (
              <div key={app.slug} className="w-72 shrink-0 snap-start">
                <AppStoreCard app={app} allApps={apps} onDetails={() => openDetails(app)} />
              </div>
            ))}
          </AppSlider>
        )}

        {!isFiltering && !isLoading && newApps.length > 0 && (
          <AppSlider title={<Trans>Newly added</Trans>}>
            {newApps.map((app) => (
              <div key={app.slug} className="w-72 shrink-0 snap-start">
                <AppStoreCard app={app} allApps={apps} onDetails={() => openDetails(app)} />
              </div>
            ))}
          </AppSlider>
        )}

        {/* All apps with category filter */}
        <section className="flex flex-col gap-6">
          <div className="flex flex-col justify-between gap-3 lg:flex-row lg:items-center">
            <h2 className="text-base font-semibold text-foreground">
              {normalizedSearch ? (
                <Trans>Search results</Trans>
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
                <AppStoreCard key={app.slug} app={app} allApps={apps} onDetails={() => openDetails(app)} />
              ))}
            </div>
          )}
        </section>
      </div>
    </AppsPageShell>
  );
}

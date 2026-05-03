import type { ReactNode } from "react";

import { t } from "@lingui/core/macro";
import { Button } from "@repo/ui/components/Button";
import { createFileRoute, Link } from "@tanstack/react-router";
import { ArrowLeftIcon, ArrowRightIcon, SearchIcon } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import { filterApps, type AppCatalogItem } from "../-components/appCatalog";
import { STORE_CATEGORIES } from "../-components/appCategories";
import { AppLogo } from "../-components/AppLogo";

export const Route = createFileRoute("/dashboard/apps/store/")({
  staticData: { trackingTitle: "App store" },
  component: AppStorePage
});

function AppStorePage() {
  const [query, setQuery] = useState("");
  const apps = useMemo(() => filterApps(query), [query]);

  useEffect(() => {
    document.title = t`App store | Nerova`;
  }, []);

  return (
    <main className="flex min-h-0 flex-1 flex-col overflow-y-auto bg-[#0f0f0f] px-7 py-6 text-white">
      <header className="flex flex-wrap items-start gap-4">
        <div>
          <h1 className="font-display text-3xl font-semibold tracking-normal">App store</h1>
          <p className="mt-1 text-lg text-white/80">Connecting people, technology and the workplace.</p>
        </div>
        <label className="ml-auto flex h-12 w-full max-w-[16.5rem] items-center gap-2 rounded-xl border border-white/20 bg-black/20 px-4 text-white/70">
          <SearchIcon className="size-5" />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search"
            className="min-w-0 flex-1 bg-transparent text-base font-medium text-white outline-none placeholder:text-white/70"
          />
        </label>
      </header>

      <StoreSection title="Featured categories">
        <div className="grid grid-cols-[repeat(auto-fit,minmax(13rem,1fr))] gap-4">
          {STORE_CATEGORIES.map((category) => (
            <Link
              key={category.name}
              to="/dashboard/apps/store"
              className="group min-h-[13.5rem] overflow-hidden rounded-[3px] border border-white/5 bg-[#232323] p-8 transition-colors hover:bg-[#2b2b2b]"
            >
              <category.Icon className="mb-7 size-16 text-white/80" />
              <h3 className="font-display text-xl font-semibold">{category.label}</h3>
              <p className="mt-4 flex items-center gap-2 text-lg font-semibold text-white/55">
                {category.description}
                <ArrowRightIcon className="size-5 transition-transform group-hover:translate-x-1" />
              </p>
            </Link>
          ))}
        </div>
      </StoreSection>

      <StoreSection title={query ? "Search results" : "Most popular"}>
        <div className="grid grid-cols-[repeat(auto-fit,minmax(18rem,1fr))] gap-4">
          {apps.map((app) => (
            <StoreAppCard key={app.slug} app={app} />
          ))}
        </div>
      </StoreSection>
    </main>
  );
}

function StoreSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mt-16">
      <div className="mb-8 flex items-center gap-3">
        <h2 className="font-display text-2xl font-semibold">{title}</h2>
        <div className="ml-auto flex gap-8 text-white/75">
          <Button type="button" variant="ghost" size="icon" className="text-white hover:bg-white/10">
            <ArrowLeftIcon className="size-6" />
          </Button>
          <Button type="button" variant="ghost" size="icon" className="text-white hover:bg-white/10">
            <ArrowRightIcon className="size-6" />
          </Button>
        </div>
      </div>
      {children}
    </section>
  );
}

function StoreAppCard({ app }: { app: AppCatalogItem }) {
  return (
    <Link
      to="/dashboard/apps/$appSlug"
      params={{ appSlug: app.slug }}
      className="group min-h-56 rounded-lg border border-white/10 bg-[#111] p-8 transition-colors hover:bg-[#181818]"
    >
      <AppLogo app={app} />
      <h3 className="mt-7 font-display text-2xl font-semibold">{app.name}</h3>
      <p className="mt-2 line-clamp-2 text-sm text-white/55">{app.shortDescription}</p>
      <div className="mt-5 flex flex-wrap gap-2">
        <span className="rounded-full bg-white/10 px-2.5 py-1 text-xs font-medium text-white/80">{app.category}</span>
        {app.installState === "installed" && (
          <span className="rounded-full bg-emerald-400/15 px-2.5 py-1 text-xs font-medium text-emerald-100">
            Installed
          </span>
        )}
      </div>
    </Link>
  );
}

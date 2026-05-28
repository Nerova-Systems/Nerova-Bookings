import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { createFileRoute } from "@tanstack/react-router";
import { BlocksIcon } from "lucide-react";
import { useState } from "react";

import { api } from "@/shared/lib/api/client";

import type { App } from "./-components/appsTypes";

import { AppRow } from "./-components/AppRow";
import { AppsPageShell } from "./-components/AppsPageShell";
import { getAppCategoryLabel, groupAppsByCategory } from "./-components/appsTypes";
import { UninstallAppDialog } from "./-components/UninstallAppDialog";

export const Route = createFileRoute("/apps/installed")({
  staticData: { trackingTitle: "Installed apps" },
  component: InstalledAppsPage
});

function InstalledAppsPage() {
  const { data, isLoading } = api.useQuery("get", "/api/apps");
  const apps = data?.apps ?? [];
  const groups = groupAppsByCategory(apps);
  const [appToUninstall, setAppToUninstall] = useState<App | null>(null);

  return (
    <AppsPageShell
      title={t`Installed apps`}
      subtitle={t`Connect your calendars and conferencing tools so bookings sync automatically and meeting links are created for you.`}
    >
      <section className="flex min-w-0 flex-col gap-6">
        {isLoading ? (
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            <Trans>Loading apps...</Trans>
          </div>
        ) : apps.length === 0 ? (
          <Empty className="min-h-48 border">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <BlocksIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No apps available</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>No connectors are registered yet. Check back once integrations are enabled.</Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          groups.map(({ category, apps: appsInCategory }) => (
            <div key={category} className="flex flex-col gap-2">
              <h2 className="text-sm font-semibold text-muted-foreground">{getAppCategoryLabel(category)}</h2>
              <div className="overflow-hidden rounded-md border">
                {appsInCategory.map((app) => (
                  <AppRow key={app.slug} app={app} allApps={apps} onUninstall={setAppToUninstall} />
                ))}
              </div>
            </div>
          ))
        )}
      </section>
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

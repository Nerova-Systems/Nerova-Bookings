import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute, Link as RouterLink } from "@tanstack/react-router";
import { ArrowLeftIcon, BlocksIcon } from "lucide-react";
import { useMemo } from "react";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";

import { AppDetailView } from "./-components/AppDetailView";
import { getVisibleApps } from "./-components/appsTypes";

export const Route = createFileRoute("/apps/$slug")({
  staticData: { trackingTitle: "App Details" },
  component: AppDetailPage
});

function AppDetailPage() {
  const { slug } = Route.useParams();
  const { data, isLoading } = api.useQuery("get", "/api/apps");
  const apps = useMemo(() => getVisibleApps(data?.apps ?? []), [data?.apps]);
  const app = useMemo(() => apps.find((candidate) => candidate.slug === slug), [apps, slug]);

  const backLink = (
    <Button variant="ghost" size="sm" className="-ml-2 w-fit text-muted-foreground" render={<RouterLink to="/apps" />}>
      <ArrowLeftIcon className="size-4" />
      <Trans>Back to App Store</Trans>
    </Button>
  );

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="72rem"
          browserTitle={app?.name ?? t`App Details`}
          title={backLink}
          subtitle=""
        >
          {isLoading ? (
            <div className="grid grid-cols-1 gap-8 lg:grid-cols-[3fr_2fr]">
              <div className="order-2 h-96 animate-pulse rounded-lg border border-border bg-card/40 lg:order-1" />
              <div className="order-1 h-80 animate-pulse rounded-xl border border-border bg-card/40 lg:order-2" />
            </div>
          ) : app === undefined ? (
            <Empty className="min-h-64 rounded-2xl border bg-card/30">
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <BlocksIcon className="size-8" />
                </EmptyMedia>
                <EmptyTitle>
                  <Trans>App not found</Trans>
                </EmptyTitle>
                <EmptyDescription>
                  <Trans>This app is unavailable or no longer exists. Browse the App Store for available apps.</Trans>
                </EmptyDescription>
                <div className="mt-4">
                  <Button render={<RouterLink to="/apps" />}>
                    <Trans>Explore App Store</Trans>
                  </Button>
                </div>
              </EmptyHeader>
            </Empty>
          ) : (
            <AppDetailView app={app} allApps={apps} />
          )}
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

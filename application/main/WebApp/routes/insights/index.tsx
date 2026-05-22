import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { useMemo } from "react";
import { z } from "zod";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";

import { BookingStatusBreakdown } from "./-components/BookingStatusBreakdown";
import { BookingVolumeChart } from "./-components/BookingVolumeChart";
import { getDefaultRange, toRangeIso } from "./-components/insightsDateRange";
import { InsightsFilters } from "./-components/InsightsFilters";
import { InsightsKpiTiles } from "./-components/InsightsKpiTiles";
import { TopEventTypesChart } from "./-components/TopEventTypesChart";
import { TopHostsList } from "./-components/TopHostsList";

const insightsSearchSchema = z.object({
  from: z.string().optional(),
  to: z.string().optional()
});

export type InsightsSearch = z.infer<typeof insightsSearchSchema>;

export const Route = createFileRoute("/insights/")({
  // The cap-insights flag gates the whole route. The sidebar hides the entry when disabled,
  // but a direct navigation must redirect rather than render an unsupported page.
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("cap-insights")) {
      throw redirect({ to: "/dashboard" });
    }
  },
  staticData: { trackingTitle: "Insights" },
  validateSearch: insightsSearchSchema,
  component: InsightsPage
});

function InsightsPage() {
  const navigate = useNavigate({ from: Route.fullPath });
  const search = Route.useSearch();

  const defaults = useMemo(() => getDefaultRange(), []);
  const from = search.from ?? defaults.from;
  const to = search.to ?? defaults.to;
  const hasCustomRange = Boolean(search.from || search.to);

  const { fromIso, toIso } = useMemo(() => toRangeIso(from, to), [from, to]);
  const queryParams = { query: { From: fromIso, To: toIso } };

  const { data: kpis, isLoading: kpisLoading } = api.useQuery("get", "/api/insights/kpis", { params: queryParams });
  const { data: bookingsOverTime, isLoading: bookingsLoading } = api.useQuery(
    "get",
    "/api/insights/bookings-over-time",
    { params: queryParams }
  );
  const { data: topEventTypes, isLoading: eventTypesLoading } = api.useQuery("get", "/api/insights/top-event-types", {
    params: queryParams
  });
  const { data: topHosts, isLoading: hostsLoading } = api.useQuery("get", "/api/insights/top-hosts", {
    params: queryParams
  });

  const handleRangeChange = (next: { from: string; to: string }) => {
    navigate({ search: { from: next.from, to: next.to }, replace: true });
  };

  const handleReset = () => {
    navigate({ search: {}, replace: true });
  };

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="80rem"
          browserTitle={t`Insights`}
          title={<Trans>Insights</Trans>}
          subtitle={t`Booking analytics, event-type performance, and member load.`}
        >
          <div className="flex flex-col gap-6">
            <InsightsFilters
              from={from}
              to={to}
              hasCustomRange={hasCustomRange}
              onChange={handleRangeChange}
              onReset={handleReset}
            />
            <InsightsKpiTiles kpis={kpis} isLoading={kpisLoading} />
            <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
              <BookingVolumeChart data={bookingsOverTime} isLoading={bookingsLoading} />
              <TopEventTypesChart data={topEventTypes} isLoading={eventTypesLoading} />
              <TopHostsList data={topHosts} isLoading={hostsLoading} />
              <BookingStatusBreakdown kpis={kpis} isLoading={kpisLoading} />
            </div>
          </div>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

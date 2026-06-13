import { t } from "@lingui/core/macro";
import { useUserInfo } from "@repo/infrastructure/auth/hooks";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute } from "@tanstack/react-router";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";
import {
  useCompletedReceptionistJobRunsQuery,
  useOpenReceptionistEscalationsQuery
} from "@/shared/lib/receptionist/queries";

import { getTimeBasedGreeting, getTodayRange } from "./-components/dashboardHelpers";
import { EmptyToday, HandledByNerova, TodaySummary, TodaysBookings } from "./-components/DashboardSections";

export const Route = createFileRoute("/dashboard/")({
  staticData: { trackingTitle: "Today" },
  component: DashboardPage
});

function DashboardPage() {
  const userInfo = useUserInfo();
  const todayRange = getTodayRange();
  const bookingsQuery = api.useQuery(
    "get",
    "/api/bookings",
    {
      params: {
        query: {
          Statuses: ["upcoming", "unconfirmed"],
          AfterStartDate: todayRange.start,
          BeforeEndDate: todayRange.end,
          PageOffset: 0,
          PageSize: 20
        }
      }
    },
    { refetchOnWindowFocus: true, refetchInterval: 15000 }
  );
  const escalationsQuery = useOpenReceptionistEscalationsQuery();
  const completedJobRunsQuery = useCompletedReceptionistJobRunsQuery(25);

  const bookings = bookingsQuery.data?.bookings ?? [];
  const needsYouCount = escalationsQuery.data?.openCount ?? 0;
  const handledInLastDay = (completedJobRunsQuery.data?.jobRuns ?? []).filter((jobRun) => {
    const timestamp = jobRun.executedAt ?? jobRun.createdAt;
    return new Date(timestamp).getTime() >= Date.now() - 24 * 60 * 60 * 1000;
  });
  const isLoading = bookingsQuery.isLoading || escalationsQuery.isLoading || completedJobRunsQuery.isLoading;
  const isQuiet = !isLoading && bookings.length === 0 && needsYouCount === 0 && handledInLastDay.length === 0;

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="64rem"
          browserTitle={t`Today`}
          title={getTimeBasedGreeting(userInfo?.firstName)}
          subtitle={t`A calm look at today's bookings and what Nerova handled for you.`}
        >
          <div className="flex flex-col gap-6">
            <TodaySummary needsYouCount={needsYouCount} handledCount={handledInLastDay.length} isLoading={isLoading} />
            <TodaysBookings bookings={bookings} isLoading={bookingsQuery.isLoading} />
            <HandledByNerova receipts={handledInLastDay} isLoading={completedJobRunsQuery.isLoading} />
            {isQuiet && <EmptyToday />}
          </div>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

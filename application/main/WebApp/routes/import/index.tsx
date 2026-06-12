import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Card, CardContent } from "@repo/ui/components/Card";
import { SidebarInset, SidebarProvider } from "@repo/ui/components/Sidebar";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";

import { MainSideMenu } from "@/shared/components/MainSideMenu";
import { api } from "@/shared/lib/api/client";

import { ImportJobReceipt } from "./-components/ImportJobReceipt";
import { ImportSkeleton } from "./-components/ImportSkeleton";
import { UploadCard } from "./-components/UploadCard";

export const Route = createFileRoute("/import/")({
  staticData: { trackingTitle: "Import clients" },
  component: ImportClientsPage
});

function ImportClientsPage() {
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null);
  const importJobsQuery = api.useQuery("get", "/api/main/import-jobs");
  const newestJob = importJobsQuery.data?.importJobs[0];
  const jobId = selectedJobId ?? newestJob?.id;
  const jobQuery = api.useQuery(
    "get",
    "/api/main/import-jobs/{id}",
    { params: { path: { id: jobId ?? "" } } },
    { enabled: !!jobId }
  );

  return (
    <SidebarProvider>
      <MainSideMenu />
      <SidebarInset>
        <AppLayout
          variant="center"
          maxWidth="64rem"
          browserTitle={t`Import clients`}
          title={t`Import clients`}
          subtitle={t`Bring your client list over and we will show you a clear receipt before anything is saved.`}
        >
          <div className="flex flex-col gap-6">
            <UploadCard onJobStarted={setSelectedJobId} />
            {importJobsQuery.isLoading || (jobId && jobQuery.isLoading) ? (
              <ImportSkeleton />
            ) : jobQuery.data ? (
              <ImportJobReceipt job={jobQuery.data} onTryAgain={() => setSelectedJobId(null)} />
            ) : newestJob ? null : (
              <Card>
                <CardContent className="pt-6 text-sm text-muted-foreground">
                  <Trans>Your import receipt will appear here after you choose a CSV file.</Trans>
                </CardContent>
              </Card>
            )}
          </div>
        </AppLayout>
      </SidebarInset>
    </SidebarProvider>
  );
}

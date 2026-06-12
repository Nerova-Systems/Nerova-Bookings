import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { Switch } from "@repo/ui/components/Switch";
import { useState } from "react";

import { api, JobRunStatus, queryClient } from "@/shared/lib/api/client";

import { ReceptionistFineTuneDialog } from "./ReceptionistFineTuneDialog";
import { ReceptionistHandledFeed } from "./ReceptionistHandledFeed";
import { enabledMessage, settingsBody, settingsQueryKey } from "./receptionistHelpers";
import { ReceptionistNeedsYouBlock } from "./ReceptionistNeedsYouBlock";

function ReceptionistCardSkeleton() {
  return (
    <Card>
      <CardHeader>
        <Skeleton className="h-6 w-2/3" />
        <Skeleton className="h-4 w-1/2" />
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Skeleton className="h-20 w-full" />
        <Skeleton className="h-28 w-full" />
      </CardContent>
    </Card>
  );
}

export function ReceptionistCard() {
  const [isFineTuneOpen, setIsFineTuneOpen] = useState(false);
  const settingsQuery = api.useQuery("get", "/api/main/receptionist/settings");
  const escalationsQuery = api.useQuery("get", "/api/main/receptionist/escalations", {
    params: { query: { OpenOnly: true } }
  });
  const awaitingJobRunsQuery = api.useQuery("get", "/api/main/autonomy/job-runs", {
    params: { query: { Status: JobRunStatus.AwaitingApproval, Limit: 5 } }
  });
  const completedJobRunsQuery = api.useQuery("get", "/api/main/autonomy/job-runs", {
    params: { query: { Status: JobRunStatus.Completed, Limit: 8 } }
  });
  const updateSettingsMutation = api.useMutation("put", "/api/main/receptionist/settings", {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: settingsQueryKey })
  });

  if (settingsQuery.isLoading) {
    return <ReceptionistCardSkeleton />;
  }

  const settings = settingsQuery.data;
  if (!settings) {
    return null;
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-4">
          <div className="flex min-w-0 flex-col gap-2">
            <div className="flex items-center gap-2">
              <span className={`size-2 rounded-full ${settings.isEnabled ? "bg-success" : "bg-muted-foreground"}`} />
              <CardTitle>{enabledMessage(settings.isEnabled)}</CardTitle>
            </div>
            <CardDescription>
              <Trans>We answer, suggest next steps, and bring you in only when a client needs your call.</Trans>
            </CardDescription>
          </div>
          <Switch
            checked={settings.isEnabled}
            disabled={updateSettingsMutation.isPending}
            aria-label={t`Turn AI receptionist on or off`}
            onCheckedChange={(isEnabled) => {
              const previous = settingsQuery.data;
              queryClient.setQueryData(settingsQueryKey, settingsBody(settings, isEnabled));
              updateSettingsMutation.mutate(
                { body: settingsBody(settings, isEnabled) },
                {
                  onError: () => {
                    if (previous) {
                      queryClient.setQueryData(settingsQueryKey, previous);
                    }
                  }
                }
              );
            }}
          />
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-6">
        <div className="flex justify-end">
          <Button variant="outline" onClick={() => setIsFineTuneOpen(true)}>
            <Trans>Fine-tune</Trans>
          </Button>
        </div>
        <ReceptionistNeedsYouBlock
          escalations={escalationsQuery.data?.escalations ?? []}
          openCount={escalationsQuery.data?.openCount ?? 0}
        />
        <ReceptionistHandledFeed
          awaitingJobRuns={awaitingJobRunsQuery.data?.jobRuns ?? []}
          completedJobRuns={completedJobRunsQuery.data?.jobRuns ?? []}
          isLoading={awaitingJobRunsQuery.isLoading || completedJobRunsQuery.isLoading}
        />
      </CardContent>
      <ReceptionistFineTuneDialog isOpen={isFineTuneOpen} settings={settings} onOpenChange={setIsFineTuneOpen} />
    </Card>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Skeleton } from "@repo/ui/components/Skeleton";
import { CheckCircle2Icon, SparklesIcon } from "lucide-react";
import { toast } from "sonner";

import { SmartDate } from "@/shared/components/SmartDate";
import { api, queryClient, type Schemas } from "@/shared/lib/api/client";

import { awaitingJobRunsQueryKey, completedJobRunsQueryKey, policiesQueryKey } from "./receptionistHelpers";

type JobRun = Schemas["JobRunResponse"];
type ApproveJobRunResponse = Schemas["ApproveJobRunResponse"];

export function ReceptionistHandledFeed({
  awaitingJobRuns,
  completedJobRuns,
  isLoading
}: Readonly<{ awaitingJobRuns: JobRun[]; completedJobRuns: JobRun[]; isLoading: boolean }>) {
  const approveMutation = api.useMutation("post", "/api/main/autonomy/job-runs/{id}/approve");
  const dismissMutation = api.useMutation("post", "/api/main/autonomy/job-runs/{id}/dismiss", {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: awaitingJobRunsQueryKey })
  });
  const policyMutation = api.useMutation("put", "/api/main/autonomy/policies", {
    onSuccess: () => {
      toast.success(t`We will handle this next time.`);
      queryClient.invalidateQueries({ queryKey: policiesQueryKey });
    }
  });

  const refreshJobRuns = () => {
    queryClient.invalidateQueries({ queryKey: awaitingJobRunsQueryKey });
    queryClient.invalidateQueries({ queryKey: completedJobRunsQueryKey });
  };

  if (isLoading) {
    return (
      <section className="flex flex-col gap-3">
        <Skeleton className="h-6 w-40" />
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-16 w-full" />
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-4">
      {awaitingJobRuns.length > 0 && (
        <AwaitingApprovalList
          jobRuns={awaitingJobRuns}
          approveMutation={approveMutation}
          dismissMutation={dismissMutation}
          policyMutation={policyMutation}
          onRefresh={refreshJobRuns}
        />
      )}
      <CompletedJobRuns jobRuns={completedJobRuns} />
    </section>
  );
}

function AwaitingApprovalList({
  jobRuns,
  approveMutation,
  dismissMutation,
  policyMutation,
  onRefresh
}: Readonly<{
  jobRuns: JobRun[];
  approveMutation: ReturnType<typeof api.useMutation>;
  dismissMutation: ReturnType<typeof api.useMutation>;
  policyMutation: ReturnType<typeof api.useMutation>;
  onRefresh: () => void;
}>) {
  return (
    <div className="flex flex-col gap-3 rounded-lg border bg-muted/30 p-4">
      <h3>
        <Trans>Ready for your yes</Trans>
      </h3>
      {jobRuns.map((jobRun) => (
        <div key={jobRun.id} className="flex flex-col gap-3 rounded-md border bg-card p-3">
          <div>
            <div className="font-medium">{jobRun.summary}</div>
            <SmartDate date={jobRun.createdAt} className="text-xs text-muted-foreground" />
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              onClick={() =>
                approveMutation.mutate(
                  { params: { path: { id: jobRun.id } } },
                  {
                    onSuccess: (response: ApproveJobRunResponse) => {
                      toast.success(response.receipt, {
                        action: response.promotionOffered
                          ? {
                              label: t`Let Nerova handle it`,
                              onClick: () => policyMutation.mutate({ body: { jobType: jobRun.jobType, level: 2 } })
                            }
                          : undefined,
                        description: response.promotionOffered
                          ? t`Want Nerova to handle this automatically from now on?`
                          : undefined
                      });
                      onRefresh();
                    }
                  }
                )
              }
              isPending={approveMutation.isPending}
            >
              <Trans>Approve</Trans>
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={() => dismissMutation.mutate({ params: { path: { id: jobRun.id } } })}
              isPending={dismissMutation.isPending}
            >
              <Trans>Dismiss</Trans>
            </Button>
          </div>
        </div>
      ))}
    </div>
  );
}

function CompletedJobRuns({ jobRuns }: Readonly<{ jobRuns: JobRun[] }>) {
  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-2">
        <SparklesIcon className="size-4 text-primary" />
        <h3>
          <Trans>Handled by Nerova</Trans>
        </h3>
      </div>
      {jobRuns.length === 0 ? (
        <div className="rounded-lg border bg-muted/30 p-4 text-sm text-muted-foreground">
          <Trans>We will show each booking helper we finish here.</Trans>
        </div>
      ) : (
        jobRuns.map((jobRun) => (
          <div key={jobRun.id} className="flex gap-3 rounded-md border p-3">
            <CheckCircle2Icon className="mt-0.5 size-4 text-success" />
            <div className="min-w-0 flex-1">
              <div className="text-sm">{jobRun.receipt ?? jobRun.summary}</div>
              <SmartDate date={jobRun.executedAt ?? jobRun.createdAt} className="text-xs text-muted-foreground" />
            </div>
          </div>
        ))
      )}
    </div>
  );
}

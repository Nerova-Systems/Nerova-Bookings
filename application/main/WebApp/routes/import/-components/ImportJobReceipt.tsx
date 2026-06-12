import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Link } from "@tanstack/react-router";
import { toast } from "sonner";

import { api, ImportJobStatus, queryClient, type Schemas } from "@/shared/lib/api/client";

import { importJobsQueryKey } from "./importHelpers";
import { ImportRowsTable } from "./ImportRowsTable";

type ImportJobDetails = Schemas["ImportJobDetailsResponse"];

export function ImportJobReceipt({ job, onTryAgain }: Readonly<{ job: ImportJobDetails; onTryAgain: () => void }>) {
  if (job.status === ImportJobStatus.Completed) {
    return <CompletedReceipt job={job} />;
  }
  if (job.status === ImportJobStatus.Failed) {
    return <FailedReceipt job={job} onTryAgain={onTryAgain} />;
  }
  if (job.status !== ImportJobStatus.ReadyForReview) {
    return <CheckingReceipt job={job} />;
  }
  return <ReadyForReviewReceipt job={job} />;
}

function CompletedReceipt({ job }: Readonly<{ job: ImportJobDetails }>) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>All set — {job.rowsCommitted} clients imported.</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>Your client list is ready for bookings, messages, and follow-ups.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Button render={<Link to="/clients" />}>
          <Trans>View clients</Trans>
        </Button>
      </CardContent>
    </Card>
  );
}

function FailedReceipt({ job, onTryAgain }: Readonly<{ job: ImportJobDetails; onTryAgain: () => void }>) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>We could not read this file.</Trans>
        </CardTitle>
        <CardDescription>{job.errorMessage ?? t`Please check the CSV and try again.`}</CardDescription>
      </CardHeader>
      <CardContent>
        <Button onClick={onTryAgain}>
          <Trans>Try again</Trans>
        </Button>
      </CardContent>
    </Card>
  );
}

function CheckingReceipt({ job }: Readonly<{ job: ImportJobDetails }>) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>We are checking {job.fileName}.</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>Your receipt will appear here when the list is ready to review.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Badge variant="secondary">{job.status}</Badge>
      </CardContent>
    </Card>
  );
}

function ReadyForReviewReceipt({ job }: Readonly<{ job: ImportJobDetails }>) {
  const approveMutation = api.useMutation("post", "/api/main/import-jobs/{id}/approve", {
    onSuccess: () => {
      toast.success(t`Import started`, { description: t`We are adding the ready clients now.` });
      queryClient.invalidateQueries({ queryKey: importJobsQueryKey });
      queryClient.invalidateQueries({ queryKey: ["get", "/api/main/import-jobs/{id}"] });
    }
  });
  const rejectMutation = api.useMutation("post", "/api/main/import-jobs/{id}/reject", {
    onSuccess: () => {
      toast.success(t`Import cancelled`);
      queryClient.invalidateQueries({ queryKey: importJobsQueryKey });
    }
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Review what we found</Trans>
        </CardTitle>
        <CardDescription>
          <Trans>This is your receipt. Nothing is saved until you choose import clients.</Trans>
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        <div className="rounded-lg border bg-muted/30 p-4 text-sm font-medium">
          <Trans>
            {job.rowsTotal} clients found · {job.rowsValid} ready · {job.rowsDuplicate} already exist · {" "}
            {job.rowsInvalid} need attention
          </Trans>
        </div>
        <ImportRowsTable rows={job.rows} />
        <div className="flex flex-wrap justify-end gap-2">
          <Button
            variant="outline"
            onClick={() => rejectMutation.mutate({ params: { path: { id: job.id } } })}
            isPending={rejectMutation.isPending}
          >
            <Trans>Cancel</Trans>
          </Button>
          <Button
            onClick={() =>
              approveMutation.mutate({ params: { path: { id: job.id } }, body: { excludeRowNumbers: [] } })
            }
            isPending={approveMutation.isPending}
          >
            <Trans>Import clients</Trans>
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

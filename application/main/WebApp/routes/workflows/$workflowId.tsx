import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { EditableHeading } from "@repo/ui/components/EditableHeading";
import { createFileRoute, redirect, useNavigate } from "@tanstack/react-router";
import { ArrowLeftIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import { DeleteWorkflowDialog } from "./-components/DeleteWorkflowDialog";
import { WorkflowApiErrors } from "./-components/WorkflowApiErrors";
import { WorkflowBindingsCard } from "./-components/WorkflowBindingsCard";
import { WorkflowsPageShell } from "./-components/WorkflowsPageShell";
import { WorkflowStepsSection } from "./-components/WorkflowStepsSection";
import { getWorkflowTriggerLabel, workflowPathId } from "./-components/workflowTypes";

export const Route = createFileRoute("/workflows/$workflowId")({
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("cap-workflows")) {
      throw redirect({ to: "/dashboard" });
    }
  },
  staticData: { trackingTitle: "Workflow details" },
  component: WorkflowDetailsPage
});

function WorkflowDetailsPage() {
  const { workflowId } = Route.useParams();
  const navigate = useNavigate();
  const [deleteOpen, setDeleteOpen] = useState(false);

  const { data: workflow, isLoading } = api.useQuery("get", "/api/workflows/{id}", {
    params: { path: { id: workflowPathId(workflowId) } }
  });

  const renameMutation = api.useMutation("put", "/api/workflows/{id}", {
    onSuccess: () => {
      toast.success(t`Workflow renamed`);
      void queryClient.invalidateQueries();
    }
  });

  if (isLoading || !workflow) {
    return (
      <WorkflowsPageShell title={t`Workflow`} subtitle={t`Loading...`}>
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>Loading workflow...</Trans>
        </div>
      </WorkflowsPageShell>
    );
  }

  const handleRename = (newName: string) => {
    renameMutation.mutate({
      params: { path: { id: workflowPathId(workflow.id) } },
      body: { workflowId: workflowPathId(workflow.id), name: newName }
    });
  };

  return (
    <WorkflowsPageShell
      title={workflow.name}
      subtitle={getWorkflowTriggerLabel(workflow.trigger)}
      maxWidth="64rem"
      titleContent={
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex items-center gap-2">
            <Button variant="ghost" size="sm" onClick={() => navigate({ to: "/workflows" })}>
              <ArrowLeftIcon />
              <Trans>Workflows</Trans>
            </Button>
          </div>
          <EditableHeading value={workflow.name} onChange={handleRename} />
        </div>
      }
      actions={
        <Button variant="outline" onClick={() => setDeleteOpen(true)}>
          <Trash2Icon />
          <Trans>Delete</Trans>
        </Button>
      }
    >
      <div className="flex flex-col gap-6">
        <WorkflowApiErrors error={renameMutation.error} />
        <Card>
          <CardHeader>
            <CardTitle>
              <Trans>Trigger</Trans>
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap items-center gap-3">
            <Badge variant="secondary">{getWorkflowTriggerLabel(workflow.trigger)}</Badge>
            {/* TODO: backend's UpdateWorkflowCommand only changes the name — trigger is read-only here. */}
            <p className="text-sm text-muted-foreground">
              <Trans>The trigger can't be changed after the workflow is created.</Trans>
            </p>
          </CardContent>
        </Card>

        <WorkflowStepsSection workflow={workflow} />
        <WorkflowBindingsCard workflow={workflow} />
      </div>

      <DeleteWorkflowDialog
        workflow={workflow}
        isOpen={deleteOpen}
        onOpenChange={setDeleteOpen}
        onDeleted={() => navigate({ to: "/workflows" })}
      />
    </WorkflowsPageShell>
  );
}

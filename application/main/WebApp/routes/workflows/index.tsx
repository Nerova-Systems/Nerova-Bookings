import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { isFeatureFlagEnabled } from "@repo/infrastructure/featureFlags/useFeatureFlag";
import { Badge } from "@repo/ui/components/Badge";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { Switch } from "@repo/ui/components/Switch";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { Link as RouterLink, createFileRoute, redirect } from "@tanstack/react-router";
import { PlusIcon, ZapIcon } from "lucide-react";
import { useState } from "react";

import { api } from "@/shared/lib/api/client";

import { CreateWorkflowDialog } from "./-components/CreateWorkflowDialog";
import { WorkflowsPageShell } from "./-components/WorkflowsPageShell";
import { getWorkflowTriggerLabel } from "./-components/workflowTypes";

export const Route = createFileRoute("/workflows/")({
  // The cap-workflows flag gates the whole route. The sidebar hides the entry when disabled,
  // but a direct navigation must redirect rather than render an unsupported page.
  beforeLoad: () => {
    if (!isFeatureFlagEnabled("cap-workflows")) {
      throw redirect({ to: "/dashboard" });
    }
  },
  staticData: { trackingTitle: "Workflows" },
  component: WorkflowsPage
});

function WorkflowsPage() {
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const { data, isLoading } = api.useQuery("get", "/api/workflows");
  const workflows = data?.workflows ?? [];

  return (
    <WorkflowsPageShell
      title={t`Workflows`}
      subtitle={t`Automate reminders, follow-ups, and notifications for your bookings.`}
      actions={
        <Button onClick={() => setIsCreateOpen(true)}>
          <PlusIcon />
          <Trans>New workflow</Trans>
        </Button>
      }
    >
      <section className="flex min-w-0 flex-col">
        {isLoading ? (
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            <Trans>Loading workflows...</Trans>
          </div>
        ) : workflows.length === 0 ? (
          <Empty className="min-h-48 border">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <ZapIcon />
              </EmptyMedia>
              <EmptyTitle>
                <Trans>No workflows yet</Trans>
              </EmptyTitle>
              <EmptyDescription>
                <Trans>Create your first workflow to automate reminders and notifications.</Trans>
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="overflow-hidden rounded-md border">
            {workflows.map((workflow) => (
              <div
                key={workflow.id}
                className="flex flex-wrap items-center gap-4 border-b p-4 last:border-b-0 hover:bg-muted/60"
              >
                <div className="min-w-0 flex-1">
                  <RouterLink
                    to="/workflows/$workflowId"
                    params={{ workflowId: workflow.id }}
                    className="block truncate text-base font-medium hover:underline"
                  >
                    {workflow.name}
                  </RouterLink>
                  <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                    <Badge variant="secondary">{getWorkflowTriggerLabel(workflow.trigger)}</Badge>
                    <span>
                      <Trans>{workflow.steps.length} steps</Trans>
                    </span>
                  </div>
                </div>
                {/* TODO: backend has no IsActive field yet — surfaced as a disabled placeholder until it ships. */}
                <Tooltip>
                  <TooltipTrigger
                    render={
                      <span className="inline-flex" aria-label={t`Active (coming soon)`}>
                        <Switch checked={true} disabled={true} readOnly={true} />
                      </span>
                    }
                  />
                  <TooltipContent>
                    <Trans>Enabling and disabling workflows is coming soon.</Trans>
                  </TooltipContent>
                </Tooltip>
                <Button
                  variant="outline"
                  size="sm"
                  render={<RouterLink to="/workflows/$workflowId" params={{ workflowId: workflow.id }} />}
                >
                  <Trans>Edit</Trans>
                </Button>
              </div>
            ))}
          </div>
        )}
      </section>
      <CreateWorkflowDialog isOpen={isCreateOpen} onOpenChange={setIsCreateOpen} />
    </WorkflowsPageShell>
  );
}

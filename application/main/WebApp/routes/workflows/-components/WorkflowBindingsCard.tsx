import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Card, CardContent, CardHeader, CardTitle } from "@repo/ui/components/Card";
import { Checkbox } from "@repo/ui/components/Checkbox";
import { Tooltip, TooltipContent, TooltipTrigger } from "@repo/ui/components/Tooltip";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { Workflow } from "./workflowTypes";

import { WorkflowApiErrors } from "./WorkflowApiErrors";

export function WorkflowBindingsCard({ workflow }: Readonly<{ workflow: Workflow }>) {
  const { data: eventTypesData } = api.useQuery("get", "/api/event-types");
  const eventTypes = eventTypesData?.eventTypes ?? [];

  const bindMutation = api.useMutation("post", "/api/workflows/{id}/bindings", {
    onSuccess: () => {
      toast.success(t`Event type linked`);
      void queryClient.invalidateQueries();
    }
  });

  const unbindMutation = api.useMutation("delete", "/api/workflows/{id}/bindings/{eventTypeId}", {
    onSuccess: () => {
      toast.success(t`Event type unlinked`);
      void queryClient.invalidateQueries();
    }
  });

  // TODO: backend has no GET endpoint listing current bindings for a workflow — until it ships
  // the checkbox state is intentionally optimistic.
  const boundEventTypeIds = new Set<string>();
  const handleToggleBinding = (eventTypeId: string, checked: boolean) => {
    if (checked) {
      bindMutation.mutate({
        params: { path: { id: workflow.id } },
        body: { workflowId: workflow.id, eventTypeId }
      });
    } else {
      unbindMutation.mutate({ params: { path: { id: workflow.id, eventTypeId } } });
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <Trans>Active on event types</Trans>
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        <WorkflowApiErrors error={bindMutation.error ?? unbindMutation.error} />
        <Tooltip>
          <TooltipTrigger
            render={
              <p className="w-fit cursor-help text-sm text-muted-foreground">
                <Trans>Existing links are not displayed yet.</Trans>
              </p>
            }
          />
          <TooltipContent>
            <Trans>The list-bindings endpoint is on the backlog. Toggle to link or unlink.</Trans>
          </TooltipContent>
        </Tooltip>
        {eventTypes.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            <Trans>No event types available.</Trans>
          </p>
        ) : (
          <ul className="flex flex-col gap-2">
            {eventTypes.map((eventType) => {
              const checked = boundEventTypeIds.has(eventType.id);
              return (
                <li key={eventType.id} className="flex items-center gap-3">
                  <Checkbox
                    id={`binding-${eventType.id}`}
                    checked={checked}
                    onCheckedChange={(value) => handleToggleBinding(eventType.id, Boolean(value))}
                  />
                  <label htmlFor={`binding-${eventType.id}`} className="text-sm">
                    {eventType.title}
                  </label>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

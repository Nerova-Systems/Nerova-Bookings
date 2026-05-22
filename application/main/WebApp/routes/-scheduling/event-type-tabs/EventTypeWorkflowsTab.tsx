import { Trans } from "@lingui/react/macro";
import { Link } from "@tanstack/react-router";

import { api } from "@/shared/lib/api/client";

import type { EventTypeTabProps } from "./EventTypeTabTypes";

import { EventTypeTabSection } from "./EventTypeTabSection";

export function EventTypeWorkflowsTab(_props: EventTypeTabProps) {
  const { data } = api.useQuery("get", "/api/workflows");
  const workflows = data?.workflows ?? [];

  return (
    <EventTypeTabSection
      title={<Trans>Workflows</Trans>}
      description={
        <Trans>
          Workflows automate booking confirmations, reminders, and follow-up messages. Bind a workflow to this event
          type from the workflow editor.
        </Trans>
      }
    >
      {workflows.length === 0 ? (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>No workflows yet. Create one to send automated booker messages.</Trans>
        </div>
      ) : (
        <ul className="grid gap-2">
          {workflows.map((workflow) => (
            <li key={workflow.id} className="rounded-md border p-3">
              <Link
                to="/workflows/$workflowId"
                params={{ workflowId: workflow.id }}
                className="font-medium hover:underline"
              >
                {workflow.name}
              </Link>
            </li>
          ))}
        </ul>
      )}
      {/* TODO: backend has no GET endpoint listing bindings per event type yet. */}
    </EventTypeTabSection>
  );
}

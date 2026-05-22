import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from "@repo/ui/components/Empty";
import { PlusIcon, ZapIcon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, queryClient } from "@/shared/lib/api/client";

import type { Workflow, WorkflowStepDraft } from "./workflowTypes";

import { WorkflowStepCard } from "./WorkflowStepCard";
import { isStepDirty, newStepDraft, nullIfBlank, stepToDraft, workflowPathId } from "./workflowTypes";

export function WorkflowStepsSection({ workflow }: Readonly<{ workflow: Workflow }>) {
  const [newStep, setNewStep] = useState<WorkflowStepDraft | null>(null);
  const [stepDrafts, setStepDrafts] = useState<Record<string, WorkflowStepDraft>>({});

  useEffect(() => {
    setStepDrafts((previous) => {
      const next: Record<string, WorkflowStepDraft> = {};
      for (const step of workflow.steps) {
        next[step.id] = previous[step.id] ?? stepToDraft(step);
      }
      return next;
    });
  }, [workflow]);

  const addStepMutation = api.useMutation("post", "/api/workflows/{id}/steps", {
    onSuccess: () => {
      toast.success(t`Step added`);
      setNewStep(null);
      void queryClient.invalidateQueries();
    }
  });

  const updateStepMutation = api.useMutation("put", "/api/workflows/{id}/steps/{stepId}", {
    onSuccess: () => {
      toast.success(t`Step updated`);
      void queryClient.invalidateQueries();
    }
  });

  const removeStepMutation = api.useMutation("delete", "/api/workflows/{id}/steps/{stepId}", {
    onSuccess: () => {
      toast.success(t`Step removed`);
      void queryClient.invalidateQueries();
    }
  });

  const handleSaveStep = (stepId: string | null, draft: WorkflowStepDraft) => {
    const payload = {
      workflowId: workflowPathId(workflow.id),
      action: draft.action,
      template: draft.template,
      reminderTime: draft.reminderTime,
      timeUnit: draft.timeUnit,
      sendTo: nullIfBlank(draft.sendTo),
      emailSubject: nullIfBlank(draft.emailSubject),
      emailBody: nullIfBlank(draft.emailBody)
    };
    if (stepId === null) {
      addStepMutation.mutate({ params: { path: { id: workflowPathId(workflow.id) } }, body: payload });
    } else {
      updateStepMutation.mutate({
        params: { path: { id: workflowPathId(workflow.id), stepId } },
        body: { ...payload, stepId }
      });
    }
  };

  const handleRemoveStep = (stepId: string | null) => {
    if (stepId === null) {
      setNewStep(null);
      return;
    }
    removeStepMutation.mutate({ params: { path: { id: workflowPathId(workflow.id), stepId } } });
  };

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">
          <Trans>Steps</Trans>
        </h2>
        <Button
          variant="outline"
          size="sm"
          onClick={() => newStep === null && setNewStep(newStepDraft(workflow.trigger))}
          disabled={newStep !== null}
        >
          <PlusIcon />
          <Trans>Add step</Trans>
        </Button>
      </div>
      {workflow.steps.length === 0 && newStep === null ? (
        <Empty className="min-h-32 border">
          <EmptyHeader>
            <EmptyMedia variant="icon">
              <ZapIcon />
            </EmptyMedia>
            <EmptyTitle>
              <Trans>No steps yet</Trans>
            </EmptyTitle>
            <EmptyDescription>
              <Trans>Add a step to send a reminder, email, or message.</Trans>
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      ) : (
        <div className="flex flex-col gap-4">
          {workflow.steps.map((step, index) => {
            const draft = stepDrafts[step.id] ?? stepToDraft(step);
            return (
              <WorkflowStepCard
                key={step.id}
                index={index}
                trigger={workflow.trigger}
                draft={draft}
                isDirty={isStepDirty(draft, step)}
                isPending={updateStepMutation.isPending}
                isRemoving={removeStepMutation.isPending}
                error={updateStepMutation.error}
                onChange={(next) => setStepDrafts((previous) => ({ ...previous, [step.id]: next }))}
                onSave={() => handleSaveStep(step.id, draft)}
                onRemove={() => handleRemoveStep(step.id)}
              />
            );
          })}
          {newStep !== null && (
            <WorkflowStepCard
              index={workflow.steps.length}
              trigger={workflow.trigger}
              draft={newStep}
              isDirty={true}
              isPending={addStepMutation.isPending}
              isRemoving={false}
              error={addStepMutation.error}
              onChange={setNewStep}
              onSave={() => handleSaveStep(null, newStep)}
              onRemove={() => handleRemoveStep(null)}
            />
          )}
        </div>
      )}
    </section>
  );
}

import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { TextField } from "@repo/ui/components/TextField";
import { useNavigate } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, queryClient, WorkflowTrigger } from "@/shared/lib/api/client";

import { WorkflowApiErrors } from "./WorkflowApiErrors";
import { getWorkflowTriggerLabel, WORKFLOW_TRIGGER_ORDER } from "./workflowTypes";

export function CreateWorkflowDialog({
  isOpen,
  onOpenChange
}: Readonly<{ isOpen: boolean; onOpenChange: (isOpen: boolean) => void }>) {
  const navigate = useNavigate();
  const defaultName = t`Untitled workflow`;
  const [name, setName] = useState(defaultName);
  const [trigger, setTrigger] = useState<WorkflowTrigger>(WorkflowTrigger.NewEvent);
  const { error, isPending, mutate, reset } = api.useMutation("post", "/api/workflows", {
    onSuccess: (workflow) => {
      toast.success(t`Workflow created`);
      void queryClient.invalidateQueries();
      onOpenChange(false);
      navigate({ to: "/workflows/$workflowId", params: { workflowId: workflow.id } });
    }
  });

  useEffect(() => {
    if (isOpen) {
      setName(defaultName);
      setTrigger(WorkflowTrigger.NewEvent);
      reset();
    }
  }, [isOpen, defaultName, reset]);

  const canSubmit = name.trim().length > 0;
  const triggerItems = WORKFLOW_TRIGGER_ORDER.map((value) => ({ value, label: getWorkflowTriggerLabel(value) }));

  return (
    <Dialog trackingTitle={t`Create workflow`} open={isOpen} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogForm
          validationErrors={error?.errors}
          onSubmit={() => {
            if (!canSubmit) return;
            mutate({ body: { name: name.trim(), trigger } });
          }}
        >
          <DialogHeader>
            <DialogTitle>
              <Trans>New workflow</Trans>
            </DialogTitle>
            <DialogDescription>
              <Trans>Name your workflow and pick the trigger that schedules its reminders.</Trans>
            </DialogDescription>
          </DialogHeader>
          <DialogBody>
            <WorkflowApiErrors error={error} />
            <TextField name="name" label={t`Name`} required={true} autoFocus={true} value={name} onChange={setName} />
            <SelectField<WorkflowTrigger>
              name="trigger"
              label={t`Trigger`}
              items={triggerItems}
              value={trigger}
              onValueChange={(value) => value !== null && setTrigger(value)}
            >
              <SelectTrigger>
                <SelectValue>
                  {(value: WorkflowTrigger) => triggerItems.find((item) => item.value === value)?.label}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {triggerItems.map((item) => (
                  <SelectItem key={item.value} value={item.value}>
                    {item.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </SelectField>
          </DialogBody>
          <DialogFooter>
            <DialogClose render={<Button type="button" variant="outline" />}>
              <Trans>Cancel</Trans>
            </DialogClose>
            <Button type="submit" disabled={!canSubmit} isPending={isPending}>
              <Trans>Create</Trans>
            </Button>
          </DialogFooter>
        </DialogForm>
      </DialogContent>
    </Dialog>
  );
}

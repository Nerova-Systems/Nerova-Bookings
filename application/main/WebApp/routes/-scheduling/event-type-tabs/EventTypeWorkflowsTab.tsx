/* eslint-disable max-lines */
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { Button } from "@repo/ui/components/Button";
import {
  DialogBody,
  DialogClose,
  DialogContent,
  DialogFooter,
  DialogForm,
  DialogHeader,
  DialogTitle
} from "@repo/ui/components/Dialog";
import { DirtyDialog } from "@repo/ui/components/DirtyDialog";
import { useDialogSetDirty } from "@repo/ui/components/DirtyDialogContext";
import { SelectContent, SelectItem, SelectTrigger, SelectValue } from "@repo/ui/components/Select";
import { SelectField } from "@repo/ui/components/SelectField";
import { Switch } from "@repo/ui/components/Switch";
import { TextAreaField } from "@repo/ui/components/TextAreaField";
import { TextField } from "@repo/ui/components/TextField";
import { MailIcon, PencilIcon, PlusIcon, Trash2Icon } from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { api, queryClient, type Schemas } from "@/shared/lib/api/client";

import { GeneralApiErrors } from "../ApiErrors";
import { SideEffectDeliveriesPanel } from "./SideEffectDeliveriesPanel";

type Workflow = Schemas["WorkflowResponse"];
type WorkflowStep = Schemas["WorkflowStep"];
type WorkflowPayload = Schemas["CreateWorkflowRequest"];

const workflowTriggers = [
  "BOOKING_CREATED",
  "BOOKING_CONFIRMED",
  "BOOKING_REJECTED",
  "BOOKING_CANCELLED",
  "BOOKING_RESCHEDULED",
  "BOOKING_LOCATION_CHANGED",
  "BOOKING_GUESTS_ADDED"
];

type EventTypeWorkflowsTabProps = Readonly<{
  eventTypeId: string;
}>;

export function EventTypeWorkflowsTab({ eventTypeId }: EventTypeWorkflowsTabProps) {
  const { data, isLoading } = api.useQuery("get", "/api/event-types/{eventTypeId}/workflows", {
    params: { path: { eventTypeId } }
  });
  const deleteMutation = api.useMutation("delete", "/api/event-types/{eventTypeId}/workflows/{id}", {
    onSuccess: () => {
      toast.success(t`Workflow deleted`);
      void queryClient.invalidateQueries();
    }
  });
  const [editingWorkflow, setEditingWorkflow] = useState<Workflow | null | undefined>(undefined);
  const workflows = data?.workflows ?? [];

  return (
    <section className="flex min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <h2>
            <Trans>Workflows</Trans>
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            <Trans>Send Cal.com-compatible booking emails when lifecycle events happen.</Trans>
          </p>
        </div>
        <Button type="button" size="sm" onClick={() => setEditingWorkflow(null)}>
          <PlusIcon />
          <Trans>Add workflow</Trans>
        </Button>
      </div>
      <GeneralApiErrors error={deleteMutation.error} />
      {isLoading ? (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>Loading workflows...</Trans>
        </div>
      ) : workflows.length === 0 ? (
        <div className="rounded-md border p-4 text-sm text-muted-foreground">
          <Trans>No workflows configured.</Trans>
        </div>
      ) : (
        <div className="grid gap-3">
          {workflows.map((workflow) => (
            <div key={workflow.id} className="flex flex-wrap items-center justify-between gap-3 rounded-md border p-4">
              <div className="flex min-w-0 items-start gap-3">
                <MailIcon className="mt-0.5 size-4 text-muted-foreground" />
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{workflow.name}</span>
                    <span className="rounded-md bg-muted px-2 py-1 text-xs text-muted-foreground">
                      {workflow.active ? t`Active` : t`Disabled`}
                    </span>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {workflow.trigger} · {workflow.steps.length} <Trans>email step</Trans>
                  </p>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="outline" size="sm" onClick={() => setEditingWorkflow(workflow)}>
                  <PencilIcon />
                  <Trans>Edit</Trans>
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={deleteMutation.isPending}
                  onClick={() => deleteMutation.mutate({ params: { path: { eventTypeId, id: workflow.id } } })}
                >
                  <Trash2Icon />
                  <Trans>Delete</Trans>
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
      <SideEffectDeliveriesPanel eventTypeId={eventTypeId} kind="email" />
      <WorkflowDialog eventTypeId={eventTypeId} workflow={editingWorkflow} onOpenChange={setEditingWorkflow} />
    </section>
  );
}

function WorkflowDialog({
  eventTypeId,
  workflow,
  onOpenChange
}: Readonly<{
  eventTypeId: string;
  workflow: Workflow | null | undefined;
  onOpenChange: (workflow: Workflow | null | undefined) => void;
}>) {
  const isOpen = workflow !== undefined;
  return (
    <DirtyDialog open={isOpen} onOpenChange={(open) => !open && onOpenChange(undefined)} trackingTitle={t`Workflow`}>
      <DialogContent className="sm:w-dialog-md">
        <DialogHeader>
          <DialogTitle>{workflow ? <Trans>Edit workflow</Trans> : <Trans>Add workflow</Trans>}</DialogTitle>
        </DialogHeader>
        {isOpen && (
          <WorkflowDialogBody eventTypeId={eventTypeId} workflow={workflow} onClose={() => onOpenChange(undefined)} />
        )}
      </DialogContent>
    </DirtyDialog>
  );
}

function WorkflowDialogBody({
  eventTypeId,
  workflow,
  onClose
}: Readonly<{
  eventTypeId: string;
  workflow: Workflow | null;
  onClose: () => void;
}>) {
  const setDirty = useDialogSetDirty();
  const [payload, setPayload] = useState<WorkflowPayload>(() => workflowToPayload(workflow));
  const createMutation = api.useMutation("post", "/api/event-types/{eventTypeId}/workflows", {
    onSuccess: () => {
      toast.success(t`Workflow saved`);
      void queryClient.invalidateQueries();
      onClose();
    }
  });
  const updateMutation = api.useMutation("put", "/api/event-types/{eventTypeId}/workflows/{id}", {
    onSuccess: () => {
      toast.success(t`Workflow saved`);
      void queryClient.invalidateQueries();
      onClose();
    }
  });
  const mutation = workflow ? updateMutation : createMutation;
  const step = payload.steps[0] ?? defaultWorkflowStep();

  useEffect(() => setPayload(workflowToPayload(workflow)), [workflow]);

  const updatePayload = (nextPayload: WorkflowPayload) => {
    setDirty(true);
    setPayload(nextPayload);
  };

  const updateStep = (nextStep: WorkflowStep) => {
    updatePayload({ ...payload, steps: [nextStep] });
  };

  return (
    <DialogForm
      validationErrors={mutation.error?.errors}
      onSubmit={() =>
        workflow
          ? updateMutation.mutate({ params: { path: { eventTypeId, id: workflow.id } }, body: payload })
          : createMutation.mutate({ params: { path: { eventTypeId } }, body: payload })
      }
    >
      <DialogBody>
        <GeneralApiErrors error={mutation.error} />
        <TextField
          autoFocus
          required
          name="name"
          label={t`Name`}
          value={payload.name}
          onChange={(name) => updatePayload({ ...payload, name })}
        />
        <div className="flex items-center justify-between gap-3 rounded-md border p-3">
          <span className="text-sm font-medium">
            <Trans>Active</Trans>
          </span>
          <Switch checked={payload.active} onCheckedChange={(active) => updatePayload({ ...payload, active })} />
        </div>
        <SelectField
          name="trigger"
          label={t`Trigger`}
          value={payload.trigger}
          onValueChange={(trigger) => updatePayload({ ...payload, trigger: trigger ?? "BOOKING_CREATED" })}
        >
          <SelectTrigger className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {workflowTriggers.map((trigger) => (
              <SelectItem key={trigger} value={trigger}>
                {trigger}
              </SelectItem>
            ))}
          </SelectContent>
        </SelectField>
        <TextField
          name="subject"
          label={t`Email subject`}
          value={step.subject ?? ""}
          onChange={(subject) => updateStep({ ...step, subject: subject.trim().length > 0 ? subject : null })}
        />
        <TextAreaField
          name="body"
          label={t`Email body`}
          value={step.body ?? ""}
          lines={5}
          onChange={(body) => updateStep({ ...step, body: body.trim().length > 0 ? body : null })}
        />
      </DialogBody>
      <DialogFooter>
        <DialogClose render={<Button type="reset" variant="secondary" disabled={mutation.isPending} />}>
          <Trans>Cancel</Trans>
        </DialogClose>
        <Button type="submit" isPending={mutation.isPending}>
          {mutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save workflow</Trans>}
        </Button>
      </DialogFooter>
    </DialogForm>
  );
}

function workflowToPayload(workflow: Workflow | null): WorkflowPayload {
  return {
    name: workflow?.name ?? "",
    active: workflow?.active ?? true,
    trigger: workflow?.trigger ?? "BOOKING_CREATED",
    scheduledOffsetMinutes: workflow?.scheduledOffsetMinutes ?? null,
    steps: workflow?.steps?.length ? workflow.steps : [defaultWorkflowStep()]
  };
}

function defaultWorkflowStep(): WorkflowStep {
  return {
    kind: "email",
    recipient: "booker",
    subject: null,
    body: null,
    metadata: null
  };
}
